using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyoutaTools.Pokemon.Gen3 {
    public class Save {
        public enum SaveFormat {
            Autodetect,
            Save1M,
            Save512K,
        }

        public enum PageType {
            MainSave,
            HallOfFame,
            TrainerHill,
            BattleRecording,
            Unknown,
        }

        public class Page {
            public MemoryStream Data;
            public PageType Type;
            public int SourcePosition;
        }

        public class MainSave {
            public uint SaveNumber;
            public uint SavePagesValid = 0;
            public Page[] Pages = new Page[0xE];
        }

        public static PageType IdentifyPageType( Stream data, int sourcePosition, long fileSize, SaveFormat sourceFormat ) {
            if ( sourceFormat == SaveFormat.Save512K ) {
                // forcing 512K layout
                switch ( sourcePosition ) {
                    case 0xE000:
                        Console.WriteLine( "Assuming page at 0x" + sourcePosition.ToString( "X5" ) + " is a hall of fame block." );
                        return PageType.HallOfFame;
                    case 0xF000:
                        Console.WriteLine( "Assuming page at 0x" + sourcePosition.ToString( "X5" ) + " is a battle recording block." );
                        return PageType.BattleRecording;
                    default:
                        Console.WriteLine( "Assuming page at 0x" + sourcePosition.ToString( "X5" ) + " is a main save block." );
                        return PageType.MainSave;
                }
            }
            if ( sourceFormat == SaveFormat.Save1M ) {
                // forcing 1M layout
                switch ( sourcePosition ) {
                    case 0x1C000:
                    case 0x1D000:
                        Console.WriteLine( "Assuming page at 0x" + sourcePosition.ToString( "X5" ) + " is a hall of fame block." );
                        return PageType.HallOfFame;
                    case 0x1E000:
                        Console.WriteLine( "Assuming page at 0x" + sourcePosition.ToString( "X5" ) + " is a Trainer Hill block." );
                        return PageType.TrainerHill;
                    case 0x1F000:
                        Console.WriteLine( "Assuming page at 0x" + sourcePosition.ToString( "X5" ) + " is a battle recording block." );
                        return PageType.BattleRecording;
                    default:
                        Console.WriteLine( "Assuming page at 0x" + sourcePosition.ToString( "X5" ) + " is a main save block." );
                        return PageType.MainSave;
                }
            }

            long origin = data.Position;
            data.Position = origin + 0xF80;
            ushort readChecksumBattleRecording = data.ReadUInt16(); // maybe??
            byte extraByteBattleRecording = data.PeekByte();
            data.Position = origin + 0xFF4;
            int saveLogicalPage = data.PeekByte();
            ushort readChecksumHallOfFame = data.ReadUInt16();
            ushort readChecksumMainSave = data.ReadUInt16();
            uint magic = data.ReadUInt32();
            uint saveNumberTmp = data.ReadUInt32();
            data.Position = origin;
            ushort calculatedChecksum = Checksum.CalculateSaveChecksum( data, 0xF80 );

            if ( magic == 0x08012025 && readChecksumMainSave == calculatedChecksum && saveLogicalPage <= 0xD && saveNumberTmp > 0 && sourcePosition <= 0x1C000 ) {
                Console.WriteLine( "Page at 0x" + sourcePosition.ToString( "X5" ) + " looks like a main save file block (save number " + saveNumberTmp + ", logical page " + saveLogicalPage + ")." );
                return PageType.MainSave;
            }

            if ( magic == 0x08012025 && readChecksumHallOfFame == calculatedChecksum ) {
                Console.WriteLine( "Page at 0x" + sourcePosition.ToString( "X5" ) + " looks like a hall of fame block." );
                return PageType.HallOfFame;
            }

            if ( ( ( fileSize == 0x10000 && sourcePosition == 0xF000 ) || ( fileSize == 0x20000 && sourcePosition == 0x1F000 ) ) ) {
                Console.WriteLine( "Assuming page at 0x" + sourcePosition.ToString( "X5" ) + " is a battle recording block." );
                return PageType.BattleRecording;
            }

            if ( fileSize == 0x20000 && sourcePosition == 0x1E000 ) {
                Console.WriteLine( "Assuming page at 0x" + sourcePosition.ToString( "X5" ) + " is a Trainer Hill block." );
                return PageType.TrainerHill;
            }

            Console.WriteLine( "Cannot identify page at 0x" + sourcePosition.ToString( "X5" ) + "." );
            return PageType.Unknown;
        }

        public static bool VerifyAndInsertSavePage( Dictionary<uint, MainSave> saves, Page page ) {
            page.Data.Position = 0xFF4;
            int saveLogicalPage = page.Data.ReadByte();
            page.Data.DiscardBytes( 1 );
            ushort readChecksum = page.Data.ReadUInt16();
            uint magic = page.Data.ReadUInt32();
            uint saveNumber = page.Data.ReadUInt32();

            // verify magic number
            if ( magic != 0x08012025 ) {
                Console.WriteLine( "Magic number of sector at 0x" + page.SourcePosition.ToString( "X5" ) + " is wrong." );
                return false;
            }

            // check if logical page is in valid range
            if ( saveLogicalPage > 0xD ) {
                Console.WriteLine( "Logical page of sector at 0x" + page.SourcePosition.ToString( "X5" ) + " is outside of valid range." );
                return false;
            }

            // verify checksum
            page.Data.Position = 0;
            ushort calculatedChecksum = Checksum.CalculateSaveChecksum( page.Data, 0xF80 );
            if ( readChecksum != calculatedChecksum ) {
                Console.WriteLine( "Checksum of sector at 0x" + page.SourcePosition.ToString( "X5" ) + " is wrong." );
                return false;
            }

            // page is valid, check if we have a save structure for this save yet and create one if we don't
            MainSave save;
            if ( saves.ContainsKey( saveNumber ) ) {
                save = saves[saveNumber];
            } else {
                save = new MainSave() { SaveNumber = saveNumber };
                saves.Add( saveNumber, save );
            }

            // store page in save structure
            save.Pages[saveLogicalPage] = page;
            save.SavePagesValid |= ( 1u << saveLogicalPage );

            return true;
        }

        public static int Execute( List<string> args ) {
            if ( args.Count < 1 ) {
                Console.WriteLine( "Usage: [--to1M/--to512K] [--forceInputLayout1M/--forceInputLayout512K] pokemon-emerald.sav [pokemon-emerald-converted.sav]" );
                return -1;
            }

            try {
                SaveFormat targetFormat = SaveFormat.Autodetect;
                SaveFormat sourceFormat = SaveFormat.Autodetect;
                int argcnt = 0;
                while ( args[argcnt].StartsWith( "--" ) ) {
                    bool exitArgs = false;
                    switch ( args[argcnt].ToLowerInvariant() ) {
                        case "--":
                            exitArgs = true;
                            break;
                        case "--to1m":
                            targetFormat = SaveFormat.Save1M;
                            break;
                        case "--to512k":
                            targetFormat = SaveFormat.Save512K;
                            break;
                        case "--forceinputlayout1m":
                            sourceFormat = SaveFormat.Save1M;
                            break;
                        case "--forceinputlayout512k":
                            sourceFormat = SaveFormat.Save512K;
                            break;
                        default:
                            Console.WriteLine( "Unrecognized option '" + args[argcnt] + "'." );
                            break;
                    }
                    ++argcnt;
                    if ( exitArgs ) {
                        break;
                    }
                }

                List<Page> pages = new List<Page>( 0x20 );
                String filename = args[argcnt++];
                using ( System.IO.Stream file = new System.IO.FileStream( filename, System.IO.FileMode.Open ) ) {
                    if ( targetFormat == SaveFormat.Autodetect ) {
                        targetFormat = file.Length == 0x20000 ? SaveFormat.Save512K : SaveFormat.Save1M;
                    }
                    for ( int i = 0; i < file.Length; i += 0x1000 ) {
                        MemoryStream ms = new MemoryStream( 0x1000 );
                        Util.CopyStream( file, ms, 0x1000 );
                        ms.Position = 0;
                        PageType type = IdentifyPageType( ms, i, file.Length, sourceFormat );
                        pages.Add( new Page() { Data = ms, Type = type, SourcePosition = i } );
                    }
                }

                MainSave newestSave;
                {
                    Dictionary<uint, MainSave> saves = new Dictionary<uint, MainSave>();
                    foreach ( Page p in pages ) {
                        if ( p.Type == PageType.MainSave ) {
                            VerifyAndInsertSavePage( saves, p );
                        }
                    }
                    List<MainSave> validSaves = new List<MainSave>();
                    foreach ( MainSave save in saves.Values ) {
                        if ( save.SavePagesValid == 0x3FFF ) {
                            Console.WriteLine( "Identified a complete valid save, number " + save.SaveNumber + "." );
                            validSaves.Add( save );
                        }
                    }
                    if ( validSaves.Count == 0 ) {
                        throw new Exception( "Failed to identify a complete valid save." );
                    }
                    newestSave = validSaves.OrderBy( x => x.SaveNumber ).Last();
                    Console.WriteLine( "Newest save is number " + newestSave.SaveNumber + ", using as output." );
                }

                HallOfFame.HallOfFameStructure hof = null;
                {
                    MemoryStream ms = new MemoryStream();
                    int count = 0;
                    foreach ( Page p in pages ) {
                        if ( p.Type == PageType.HallOfFame ) {
                            p.Data.Position = 0;
                            Util.CopyStream( p.Data, ms, 0x1000 );
                            ++count;
                        }
                    }
                    if ( count > 0 ) {
                        hof = HallOfFame.ReadHallOfFameFromSave( ms, 0, count );
                    }

                    bool valid = false;
                    if ( hof?.Entries != null ) {
                        int validEntries = hof.Entries.Count( x => x.IsValid() );
                        if ( validEntries > 0 ) {
                            valid = true;
                            Console.WriteLine( "Found " + validEntries + " valid hall of fame entries." );
                        }
                    }

                    if ( !valid ) {
                        hof = null;
                        Console.WriteLine( "Found no hall of fame data." );
                    }
                }

                Page trainerHill = null;
                {
                    foreach ( Page p in pages ) {
                        if ( p.Type == PageType.TrainerHill ) {
                            trainerHill = p;
                            Console.WriteLine( "Using page at 0x" + p.SourcePosition.ToString( "X5" ) + " as Trainer Hill data." );
                            break;
                        }
                    }
                }

                Page battleRecording = null;
                {
                    foreach ( Page p in pages ) {
                        if ( p.Type == PageType.BattleRecording ) {
                            battleRecording = p;
                            Console.WriteLine( "Using page at 0x" + p.SourcePosition.ToString( "X5" ) + " as battle recording." );
                            break;
                        }
                    }
                }

                // combine into one large stream
                MemoryStream targetStream;
                {
                    targetStream = new MemoryStream();
                    foreach ( Page p in newestSave.Pages ) {
                        p.Data.Position = 0;
                        Util.CopyStream( p.Data, targetStream, 0x1000 );
                    }
                    if ( targetFormat == SaveFormat.Save1M ) {
                        // second copy of save
                        foreach ( Page p in newestSave.Pages ) {
                            p.Data.Position = 0;
                            Util.CopyStream( p.Data, targetStream, 0x1000 );
                        }
                    }

                    if ( hof != null && targetFormat == SaveFormat.Save512K ) {
                        // truncate hall of fame to 32 entries
                        int validHallOfFameEntires = hof.Entries.Count( x => x.IsValid() );
                        int toSkip;
                        if ( validHallOfFameEntires > 32 ) {
                            toSkip = validHallOfFameEntires - 32;
                            Console.WriteLine( "Removing the " + toSkip + " oldest Hall of Fame entries to fit it into smaller save file." );
                        } else {
                            toSkip = 0;
                        }
                        List<HallOfFame.HallOfFameEntry> entriesToKeep = new List<HallOfFame.HallOfFameEntry>();
                        for ( int i = 0; i < hof.Entries.Length; ++i ) {
                            if ( hof.Entries[i].IsValid() ) {
                                if ( toSkip > 0 ) {
                                    --toSkip;
                                } else {
                                    entriesToKeep.Add( hof.Entries[i] );
                                }
                            }
                        }
                        while ( entriesToKeep.Count < 32 ) {
                            entriesToKeep.Add( new HallOfFame.HallOfFameEntry() );
                        }
                        hof.Entries = entriesToKeep.ToArray();
                    }

                    if ( hof != null ) {
                        HallOfFame.WriteHallOfFameDataToSave( hof, targetStream, targetFormat == SaveFormat.Save512K ? 0xE000 : 0x1C000, targetFormat == SaveFormat.Save512K ? 1 : 2 );
                    } else {
                        Console.WriteLine( "Didn't find Hall of Fame, writing blank data instead." );
                        Util.WriteAlign( targetStream, targetFormat == SaveFormat.Save512K ? 0xF000 : 0x1E000, 0xFF );
                    }

                    if ( targetFormat == SaveFormat.Save1M ) {
                        if ( trainerHill != null ) {
                            trainerHill.Data.Position = 0;
                            Util.CopyStream( trainerHill.Data, targetStream, 0x1000 );
                        } else {
                            Console.WriteLine( "Didn't find Trainer Hill data, writing blank data instead." );
                            Util.WriteAlign( targetStream, 0x1F000, 0xFF );
                        }
                    }

                    if ( battleRecording != null ) {
                        battleRecording.Data.Position = 0;
                        Util.CopyStream( battleRecording.Data, targetStream, 0x1000 );
                    } else {
                        Console.WriteLine( "Didn't find Battle Recording data, writing blank data instead." );
                        Util.WriteAlign( targetStream, targetFormat == SaveFormat.Save512K ? 0x10000 : 0x20000, 0xFF );
                    }

                    // sanity check
                    switch ( targetFormat ) {
                        case SaveFormat.Save1M:
                            if ( targetStream.Length != 0x20000 ) { throw new Exception( "Output has wrong size, something went wrong." ); }
                            break;
                        case SaveFormat.Save512K:
                            if ( targetStream.Length != 0x10000 ) { throw new Exception( "Output has wrong size, something went wrong." ); }
                            break;
                    }
                }


                {
                    String outname;
                    if ( args.Count > argcnt ) {
                        outname = args[argcnt++];
                    } else {
                        String fnabs = System.IO.Path.GetFullPath( filename );
                        outname = System.IO.Path.Combine( System.IO.Path.GetDirectoryName( fnabs ), System.IO.Path.GetFileNameWithoutExtension( fnabs ) + "_" + ( targetFormat == SaveFormat.Save1M ? "1Mb" : "512Kb" ) + System.IO.Path.GetExtension( fnabs ) );
                    }
                    Console.WriteLine( "Writing converted save file to " + outname + "..." );
                    using ( System.IO.Stream outfile = new System.IO.FileStream( outname, System.IO.FileMode.Create ) ) {
                        targetStream.Position = 0;
                        Util.CopyStream( targetStream, outfile, (int)targetStream.Length );
                    }
                }
            } catch ( Exception ex ) {
                Console.WriteLine( "Error: " + ex.ToString() );
                return -1;
            }

            return 0;
        }
    }
}
