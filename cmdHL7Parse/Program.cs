﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;

namespace cmdHL7Parse
{
    public class ConfigParse
    {
        public String key { get; set; }
        public String flag { get; set; }
        public List<extension_test> extension_tests { get; set; }
        public bool any_test { get; set; }
        public int year_limit { get; set; }
        public String[] PregnancyTest { get; set; }
    }
    public class extension_test
    {
        public string panel_id { get; set; }
        public string OBR15 { get; set; }
    }


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
        //    FileName = args[ 0 ];*/

            FileName = "rprresult_201822Mo_133802.exp";

            var dd = HL7.ParseMSG( FileName );

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

        private static List<ConfigParse> Config()
        {
            var result = new List<ConfigParse>();
            foreach (var line in File.ReadAllLines("config.ini").Where(s => !String.IsNullOrEmpty(s)).Where(s => s.Trim()[0] != ';').ToArray())
            {
                var config_line = new ConfigParse();

                string[] cline_split = line.Split('|');
                string[] configkay = cline_split[0].Split('^');

                config_line.key = configkay[0];
                config_line.flag = configkay[1];
                var extension_tests = cline_split[1].Contains('^') ? cline_split[1].Split('^') : cline_split[1].Split('#');
                config_line.extension_tests = extension_tests.Select(s => new extension_test
                {
                    panel_id = s.Any(an => an == '(') ? s.Substring(0, s.IndexOf('(')) : s,
                    OBR15 = s.Contains('(') ? s.Substring(s.IndexOf('(') + 1, s.IndexOf(')') - s.IndexOf('(')-1) : ""
                }).ToList();
                config_line.any_test = cline_split[1].Contains('^') || config_line.extension_tests.Count() == 1;
                config_line.year_limit = cline_split.Count() > 2 ? Convert.ToInt32(cline_split[2]) : -1;
                config_line.PregnancyTest = cline_split.Count() == 4 ? cline_split[3].Split('^') : new string[] { };

                result.Add(config_line);
            }

            return result;
        }

        static List<string> temp_log = new List<string>();
        static List<H_MSG> history = History.ParseMSG("History.hl7");

        private static void GetData( List<MSG> msg )
        {
            int countMsg = msg.Count(w => w.bad_pid_name == false);

            foreach ( var line in Config() )
            {
                int a = 0;//, pr = 0;
                foreach (var msg_item in msg.Where(w => w.bad_pid_name == false))
                {
                    temp_log.Clear();
                    foreach ( var obr_item in msg_item.OBRs )
                    {
                        foreach ( var zlr_item in obr_item.ZLRs )
                        {
                            foreach ( var obx_item in zlr_item.OBXs )
                            {
                                var h =
                                    history.Where(
                                        w =>
                                            w.pid_id == msg_item.pid_id && line.extension_tests.Any(an=> an.panel_id == w.obx_id ) &&
                                            w.delete == false );

                                if ( ( ( line.key == obx_item.obx_id && line.flag == obx_item.abnormal_flag ) || h.Any() ) &&
                                     ( line.year_limit == -1 || msg_item.pid_year <= line.year_limit ) )
                                {
                                    CheckResults( msg.Where( w => w.pid_id == msg_item.pid_id ).ToList(), line, h );
                                }
                            }
                        }
                    }

                    int d = 100*++a/countMsg;

               //     if ( d != pr )
             //       {
                        Console.WriteLine( d + "%" );
              //          pr = d;
              //      }
                }
            }

            Historylog( String.Join( Environment.NewLine,
                history.Where( w => w.delete == false ).Select( s => s.MSH_Block ).ToArray() ) );
        }


        static void CheckResults(List<MSG> msg,  ConfigParse config_line, IEnumerable<H_MSG> _history)
        {
            var keys = GetConfig().Select( s => s.Split( '|' )[ 0 ].Split( '^' )[ 0 ] ).ToArray();

            foreach (var item in _history)
            {
                temp_log.Add( item.MSH_Block );
                item.delete = true;
            }

            foreach (var msg_item in msg)
            {
                bool pt = PregnancyTest(config_line.PregnancyTest, msg_item.OBRs.ToList());

                foreach (var obr_item in msg_item.OBRs)
                {
                    foreach (var zlr_item in obr_item.ZLRs)
                    {
                        String logobx = "";
                        foreach (var obx_item in zlr_item.OBXs)
                        {
                            string[] ObservationIdentifier = obx_item.OBX_split_heder[3].Split('^');

                            String id_test = obx_item.obx_id;

                            if (pt && keys.Contains(id_test)) //
                            {
                                obr_item.OBR_split[13] = "PROBABLE PREGNANCY";
                                obr_item.OBR_line = String.Join("|", obr_item.OBR_split);
                            }

                            var tmp_ = obx_item.OBX_split_heder.ToArray();
                            if (config_line.extension_tests.Select(s=> s.panel_id).Contains(id_test))
                            {
                                String oi_1 = ObservationIdentifier[0];
                                String oi_4 = ObservationIdentifier[3];

                                String oi_3 = ObservationIdentifier[2];
                                String oi_6 = ObservationIdentifier[5];
                                ObservationIdentifier[0] = oi_4;
                                ObservationIdentifier[3] = oi_1;

                                ObservationIdentifier[2] = oi_6;
                                ObservationIdentifier[5] = oi_3;
                                /* obx_item.OBX_split_heder[3]*/
                                tmp_[3] = String.Join("^", ObservationIdentifier);

                                if ( !obx_item.is_loged )
                                {
                                    logobx += String.Join( "|", /*obx_item.OBX_split_heder*/tmp_ ).Replace("Lenco^33D1012663^CLIA", "33D1012663^Lenco^CLIA") +
                                              Environment.NewLine;
                                    obx_item.is_loged = true;
                                }

                                var i = config_line.extension_tests.FirstOrDefault(w => w.panel_id == id_test);
                                if (i != null && !string.IsNullOrEmpty(i.OBR15))
                                {
                                    obr_item.OBR_split[15] = i.OBR15;
                                }
                            }
                        }

                        if (logobx.Length > 0)
                        {
                            String msgstr = msg_item.MSH_line + Environment.NewLine + msg_item.PID_line +
                                            Environment.NewLine + obr_item.OBR_line + Environment.NewLine +
                                            zlr_item.ZLR_heder + Environment.NewLine + logobx;

                            string[] address = msg_item.PID_split[11].Split(new char[] { '^' }, StringSplitOptions.RemoveEmptyEntries);
                            
                            if ( address.Length < 4 )
                            {
                                errlog( msgstr );
                            }

                            //log(msgstr);
                            temp_log.Add(msgstr);
                        }
                    }
                }
            }

            var str = String.Join(Environment.NewLine, temp_log.ToArray());

            if (temp_log.Count() != config_line.extension_tests.Count() && !config_line.any_test)
            {
                history.Add(new H_MSG() { MSH_Block = str, delete = false });
            }
            else
            {
                log(str);
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
            StreamWriter sw = new StreamWriter(fileNameHistory, false);
            sw.WriteLine(msg);
            sw.Close();
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


    }
}
