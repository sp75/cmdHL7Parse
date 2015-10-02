using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;

namespace cmdHL7Parse
{

    class Program
    {
        static string FileName;

        private static void Main( string[] args )
        {

            Console.WriteLine( "*** HL7 Parse utility ***" );
            foreach ( var s in args )
            {
                Console.WriteLine( s );
            }

            if ( !args.Any() )
            {
                Console.WriteLine( "ERROR: You must specify full path to HL7 files!" );
                return;
            }

            if ( !File.Exists( args[ 0 ] ) )
            {
                Console.WriteLine( "ERROR: File {0} doesn't exist", args[ 0 ] );
                return;
            }

            if ( !File.Exists( "config.ini" ) )
            {
                Console.WriteLine( "ERROR: File config.ini doesn't exist" );
                return;
            }
            FileName = args[ 0 ];

            //    FileName = "RPRresult_for testing probable pregnancy.exp";

            var dd = HL7.ParseMSG( FileName );
            GetDataFromHistory( dd );
            GetData( dd );

            Console.WriteLine( "" );
            Console.WriteLine( "The utility has finished its work" );
        }


        private static string[] GetConfig()
        {
            return
                File.ReadAllLines( "config.ini" )
                    .Where( s => !String.IsNullOrEmpty( s ) ).Where( s => s.Trim()[ 0 ] != ';' )
                    .ToArray();
        }

        private static void GetDataFromHistory(List<MSG> msg)
        {
            if (!File.Exists("History.hl7")) return;

            string[] configLines = File.ReadAllLines("History.hl7");
            int a = 0, countMsg = configLines.Count(), pr = 0;

            foreach (string cline in configLines)
            {
                if (!String.IsNullOrEmpty(cline))
                {
                    string[] clinesplit = cline.Split('|');
                    if (clinesplit[1].IndexOf('^') > 0)
                        CheckOtherResults2(msg,
                            clinesplit[0],
                            clinesplit[1].Remove(0, clinesplit[1].IndexOf('^')).Split('^'),
                            clinesplit[1], new string[] { });
                }

                int d = 100 * ++a / countMsg;

                if (d != pr)
                {
                    Console.WriteLine(d + "%");
                    pr = d;
                }
            }
        }

        static List<string> _obx = new List<string>();
        private static void GetData( List<MSG> msg )
        {
            int a = 0, countMsg = msg.Count, pr = 0;

            var configLines = GetConfig();

            foreach ( var msg_item in msg )
            {
                _obx.Clear();
                string[] p_name = msg_item.PID_split[ 5 ].Split( '^' );

                int y = !String.IsNullOrEmpty( msg_item.PID_split[ 7 ] )
                    ? CalculateAge(
                        DateTime.ParseExact( msg_item.PID_split[ 7 ], "yyyyMMdd", CultureInfo.InvariantCulture ),
                        DateTime.Now )
                    : 150;

                if ( p_name.Count() >= 2 )
                {
                    String f_name = p_name[ 0 ].Trim();
                    String l_name = p_name[ 1 ].Trim();
                    var r = new Regex( @"\d+" ); // если в имени нет числа то обрабатываем
                    if ( !r.IsMatch( f_name ) && !r.IsMatch( l_name ) && !f_name.ToLower().Contains( "proficiency" ) &&
                         !l_name.ToLower().Contains( "proficiency" ) )
                    {

                        foreach ( var obr_item in msg_item.OBRs )
                        {
                            foreach ( var zlr_item in obr_item.ZLRs )
                            {
                                foreach ( var obx_item in zlr_item.OBXs )
                                {
                                    var test = obx_item.OBX_split_heder[ 3 ].Split( '^' )[ 0 ];

                                    foreach ( string line in configLines )
                                    {

                                        string[] clinesplit = line.Split( '|' );
                                        string[] configkay = clinesplit[ 0 ].Split( '^' );
                                        int config_year = clinesplit.Count() > 2
                                            ? Convert.ToInt32( clinesplit[ 2 ] )
                                            : -1;
                                        if ( configkay[ 0 ] == test &&
                                             configkay[ 1 ] == obx_item.OBX_split_heder[ 8 ] &&
                                             ( config_year == -1 || y <= config_year ) )
                                        {
                                            CheckOtherResults2( msg,
                                                msg_item.PID_split[ 2 ],
                                                clinesplit[ 1 ].Split( '^' ),
                                                clinesplit[ 1 ],
                                                clinesplit );
                                        }

                                    }
                                }
                            }
                        }
                    }
                }

                int d = 100*++a/countMsg;

                if ( d != pr )
                {
                    Console.WriteLine( d + "%" );
                    pr = d;
                }
            }

        }

        static void CheckOtherResults2(List<MSG> msg, string pid, string[] Tests, string test, string[] confog_line)
        {
            string key = confog_line.Any() ? confog_line[0].Split('^')[0] : "";
            var keys = GetConfig().Select( s => s.Split( '|' )[ 0 ].Split( '^' )[ 0 ] ).ToArray();
            foreach ( var msg_item in msg )
            {
                if ( msg_item.PID_split[ 2 ] == pid )
                {
                    bool pt = confog_line.Count() == 4
                        ? PregnancyTest( confog_line[ 3 ].Split( '^' ), msg_item.OBRs )
                        : false;

                    foreach (var obr_item in msg_item.OBRs)
                    {
                        foreach (var zlr_item in obr_item.ZLRs)
                        {
                            String logobx = "";
                            foreach ( var obx_item in zlr_item.OBXs )
                            {
                                string[] ObservationIdentifier = obx_item.OBX_split_heder[ 3 ].Split( '^' );
                                String id_test = ObservationIdentifier[ 0 ];

                                if ( pt && keys.Contains( id_test ) ) //
                                {
                                    obr_item.OBR_split[ 13 ] = "PROBABLE PREGNANCY";
                                    obr_item.OBR_line = String.Join( "|", obr_item.OBR_split );
                                }
                                var tmp_ = obx_item.OBX_split_heder.ToArray();
                                if ( Tests.Contains( id_test ) )
                                {
                                    String oi_1 = ObservationIdentifier[ 0 ];
                                    String oi_4 = ObservationIdentifier[ 3 ];

                                    String oi_3 = ObservationIdentifier[ 2 ];
                                    String oi_6 = ObservationIdentifier[ 5 ];
                                    ObservationIdentifier[ 0 ] = oi_4;
                                    ObservationIdentifier[ 3 ] = oi_1;

                                    ObservationIdentifier[ 2 ] = oi_6;
                                    ObservationIdentifier[ 5 ] = oi_3;
                                    /* obx_item.OBX_split_heder[3]*/
                                    tmp_[ 3 ] = String.Join( "^", ObservationIdentifier );

                                    if ( !_obx.Contains( id_test ) )
                                    {
                                        logobx += String.Join( "|", /*obx_item.OBX_split_heder*/tmp_ ) +
                                                  Environment.NewLine;
                                        _obx.Add( id_test );
                                    }
                                }
                            }

                            if (logobx.Length > 0)
                            {
                                String msgstr = msg_item.MSH_line + Environment.NewLine + msg_item.PID_line +
                                                Environment.NewLine + obr_item.OBR_line + Environment.NewLine +
                                                zlr_item.ZLR_heder + Environment.NewLine + logobx;

                                string[] address = msg_item.PID_split[11].Split(new char[] { '^' },
                                    StringSplitOptions.RemoveEmptyEntries);
                                if (address.Length < 4)
                                    errlog(msgstr);

                                log(msgstr);

                                Historylog(msg_item.PID_split[2] + "|" + test);
                            }
                        }
                    }
                }
            }
        }

        private static bool PregnancyTest( string[] confog_line, List<OBR> OBRs )
        {
            bool result = ( confog_line.Count() > 0 );

            foreach ( var test in confog_line )
            {
                if ( result )
                {
                    result = OBRs.Select( s => s.OBR_split[ 4 ].Split( '^' )[ 0 ] ).ToArray().Contains( test );
                }
            }
            return result;
        }



        private static void Historylog(string msg)
        {
            string path = Directory.GetCurrentDirectory();
            string fileNameHistory = Path.Combine(path, "History.hl7");

            if (File.Exists(fileNameHistory))
            {
                if (!File.ReadAllLines(fileNameHistory).Contains(msg))
                {
                    StreamWriter sw = new StreamWriter(fileNameHistory, true);
                    sw.WriteLine(msg);
                    sw.Close();
                }
            }
            else
            {
                StreamWriter sw = new StreamWriter(fileNameHistory, true);
                sw.WriteLine(msg);
                sw.Close();
            }
        }

        static void log(string msg)
        {
            string path = Directory.GetCurrentDirectory();
            string fileName = Path.Combine(path, "log.hl7");

            StreamWriter sw = new StreamWriter(fileName, true);
            sw.WriteLine(msg);
            sw.Close();

            Console.Write('*');
        }

        static void errlog(string msg)
        {
            string path = Directory.GetCurrentDirectory();
            string fileName = Path.Combine(path, "errlog.hl7");

            StreamWriter sw = new StreamWriter(fileName, true);
            sw.WriteLine(msg);
            sw.Close();

            Console.Write('E');
        }

        static int CalculateAge(DateTime birthDate, DateTime now)
        {
            int age = now.Year - birthDate.Year;
            if (now.Month < birthDate.Month || (now.Month == birthDate.Month && now.Day < birthDate.Day)) age--;
            return age;
        }
    }
}
