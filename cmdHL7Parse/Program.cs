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
        static List<String> listmsg;

        static Dictionary<String, String[]> config = new Dictionary<string, String[]>();

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

          //      FileName = "RPRresult(1).exp";

            var dd = HL7.ParseMSG(FileName);
            GetDataFromHistory3( dd );
            GetData3( dd );

            Console.WriteLine( "" );
            Console.WriteLine( "The utility has finished its work" );


            /*
             listmsg = GetMSG(FileName);
            GetDataFromHistory();
            GetData2();
             */
        }

        private static void GetDataFromHistory3(List<MSG> msg)
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

        private static void GetData3( List<MSG> msg )
        {
            int a = 0, countMsg = msg.Count, pr = 0;

            string[] configLines = File.ReadAllLines( "config.ini" );

            foreach ( var msg_item in msg )
            {
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
                                        if ( !String.IsNullOrEmpty( line ) )
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
            string[] p_4 = confog_line[3].Split('^');

            foreach ( var msg_item in msg )
            {
                if ( msg_item.PID_split[ 2 ] == pid )
                {
                    bool pt = PregnancyTest( p_4, msg_item.OBRs );

                    foreach ( var obr_item in msg_item.OBRs )
                    {
                        if (pt)
                        {
                            obr_item.OBR_split[13] = "PROBABLE PREGNANCY";
                            obr_item.OBR_line = String.Join("|", obr_item.OBR_split);
                        }

                        foreach ( var zlr_item in obr_item.ZLRs )
                        {
                            String logobx = "";
                            foreach (var obx_item in zlr_item.OBXs)
                            {
                                string[] ObservationIdentifier = obx_item.OBX_split_heder[3].Split('^');
                                String id_test = ObservationIdentifier[0];
                                
                                if (Tests.Contains(id_test))
                                {
                                    String oi_1 = ObservationIdentifier[0];
                                    String oi_4 = ObservationIdentifier[3];

                                    String oi_3 = ObservationIdentifier[2];
                                    String oi_6 = ObservationIdentifier[5];
                                    ObservationIdentifier[0] = oi_4;
                                    ObservationIdentifier[3] = oi_1;

                                    ObservationIdentifier[2] = oi_6;
                                    ObservationIdentifier[5] = oi_3;
                                    obx_item.OBX_split_heder[3] = String.Join("^", ObservationIdentifier);

                                    logobx += String.Join("|", obx_item.OBX_split_heder) + Environment.NewLine;
                                }
                             
                            }

                            if ( logobx.Length > 0 )
                            {
                                String msgstr = msg_item.MSH_line + Environment.NewLine + msg_item.PID_line +
                                                Environment.NewLine + obr_item.OBR_line + Environment.NewLine +
                                                zlr_item.ZLR_heder + Environment.NewLine + logobx;

                                string[] address = msg_item.PID_split[ 11 ].Split( new char[] { '^' },
                                    StringSplitOptions.RemoveEmptyEntries );
                                if ( address.Length < 4 )
                                    errlog( msgstr );

                                log( msgstr );

                                Historylog( msg_item.PID_split[ 2 ] + "|" + test );
                            }
                        }
                    }
                }
            }
        }

        static bool PregnancyTest(string[] confog_line, List<OBR> OBRs)
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

        static List<String> GetMSG(String FileName)
        {
            string[] lines = File.ReadAllLines(FileName);
            List<String> listmsg = new List<String>();
            String msgPack = "";
            bool tr = false;
            foreach (string line in lines)
            {
                string[] lineSplit = line.Split('|');
                if (tr)
                {
                    if (line.Contains(@"MSH|^~\&|"))
                    {
                        tr = false;
                        listmsg.Add(msgPack);
                        msgPack = "";
                    }
                    else msgPack += line + Environment.NewLine;


                }
                if (line.Contains(@"MSH|^~\&|") && !tr)
                {
                    tr = true;
                    msgPack += line + Environment.NewLine;

                }
            }
            if (msgPack.Length > 0) listmsg.Add(msgPack);

            return listmsg;
        }

        static Dictionary<String, List<String[]>> GetPackages(String lines)
        {
            Dictionary<String, List<String[]>> dictionary = new Dictionary<string, List<String[]>>();
            List<String[]> MSH = new List<string[]>();
            List<String[]> PID = new List<string[]>();
            List<String[]> OBR = new List<string[]>();
            List<String[]> ZLR = new List<string[]>();
            List<String[]> OBX = new List<string[]>();

            List<String[]> strMSH = new List<string[]>();
            List<String[]> strPID = new List<string[]>();
            List<String[]> strOBR = new List<string[]>();
            List<String[]> strZLR = new List<string[]>();
            List<String[]> strOBX = new List<string[]>();


            StringReader strReader = new StringReader(lines);

            while (true)
            {
                string aLine = strReader.ReadLine();
                if (aLine != null)
                {
                    string[] lineSplit = aLine.Split('|');
                    if (aLine.Substring(0, 3).Contains("MSH"))
                    {
                        MSH.Add(aLine.Split('|'));
                        strMSH.Add(new string[] { aLine });
                    }
                    else if (aLine.Substring(0, 3).Contains("PID"))
                    {
                        lineSplit[10] = "U";  //1. PID10.1 - put letter U
                        lineSplit[22] = "U";  //2. PID22.1 - put letter U
                        String tmpPID = "";
                        for (int p = 0; p < lineSplit.Count(); p++)
                        {
                            tmpPID += lineSplit[p] + "|";
                        }
                        tmpPID = tmpPID.Substring(0, tmpPID.LastIndexOf('|'));


                        PID.Add(tmpPID.Split('|'));
                        strPID.Add(new string[] { tmpPID });
                    }
                    else if (aLine.Substring(0, 3).Contains("OBR"))
                    {
                        lineSplit[22] = lineSplit[14]; //3. OBR22.1 - put the same value as in OBR14.1
                        String tmpOBR = "";
                        for (int p = 0; p < lineSplit.Count(); p++)
                        {
                            tmpOBR += lineSplit[p] + "|";
                        }
                        tmpOBR = tmpOBR.Substring(0, tmpOBR.LastIndexOf('|'));
                        

                        OBR.Add(tmpOBR.Split('|'));
                        strOBR.Add(new string[] { tmpOBR });
                    }
                    else if (aLine.Substring(0, 3).Contains("ZLR"))
                    {
                        lineSplit[1] = lineSplit[3]; //4. ZLR1.1 - put the same value as ZLR3.1
                                                       //5. ZLR1.3 - put the same value as ZLR3.3
                                                       //6. ZLR1.4 - put the same value as ZLR3.4
                                                       //7. ZLR1.5 - put the same value as ZLR3.5 
                        String tmpZLR = "";
                        for (int p = 0; p < lineSplit.Count(); p++)
                        {
                            tmpZLR += lineSplit[p] + "|";
                        }
                        tmpZLR = tmpZLR.Substring(0, tmpZLR.LastIndexOf('|'));


                        ZLR.Add(tmpZLR.Split('|'));
                        strZLR.Add(new string[] { tmpZLR });
                    }
                    else if (aLine.Substring(0, 3).Contains("OBX"))
                    {
                        OBX.Add(lineSplit);
                        strOBX.Add(new string[] { aLine });
                    }
                }
                else break;
            }

            dictionary.Add("MSH", MSH);
            dictionary.Add("strMSH", strMSH);
            dictionary.Add("PID", PID);
            dictionary.Add("strPID", strPID);
            dictionary.Add("OBR", OBR);
            dictionary.Add("strOBR", strOBR);
            dictionary.Add("ZLR", ZLR);
            dictionary.Add("strZLR", strZLR);
            dictionary.Add("OBX", OBX);
            dictionary.Add("strOBX", strOBX);
            return dictionary;
        }

        private static void GetDataFromHistory()
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
                        CheckOtherResults(clinesplit[0], clinesplit[1].Remove(0, clinesplit[1].IndexOf('^')).Split('^'), clinesplit[1]);
                }

                int d = 100*++a/countMsg;

                if (d != pr)
                {
                    Console.WriteLine(d + "%");
                    pr = d;
                }
            }
        }

        static void GetData2()
        {
        //    listmsg = GetMSG(FileName);
            int a = 0, countMsg = listmsg.Count, pr = 0;

            string[] configLines = File.ReadAllLines("config.ini");


            foreach (String line in listmsg)
            {
                Dictionary<String, List<String[]>> msg = GetPackages(line);
                string[] msh = msg["MSH"][0];
                string[] pidS = msg["PID"][0];
                string[] obrS = msg["OBR"][0];
                List<String[]> obxS = msg["OBX"];
                
                string [] p_name =pidS[5].Split('^');
                if (p_name.Count() >= 2)
                {
                    String f_name = p_name[0].Trim(); 
                    String l_name = p_name[1].Trim();
              
                    var r = new Regex(@"\d+"); // если в имени нет числа то обрабатываем
                    if (!r.IsMatch(f_name) && !r.IsMatch(l_name) && !f_name.ToLower().Contains("proficiency") && !l_name.ToLower().Contains("proficiency")) 
                    {

                        for (int i = 0; i < obxS.Count; i++)
                        {
                            string[] oItem = obxS[i];

                            var test = oItem[3].Split('^')[0];

                            foreach (string cline in configLines)
                            {
                                if (!String.IsNullOrEmpty(cline))
                                {
                                    string[] clinesplit = cline.Split('|');
                                    string[] configkay = clinesplit[0].Split('^');
                                    if (configkay[0] == test && configkay[1] == oItem[8])
                                        CheckOtherResults(pidS[2], clinesplit[1].Split('^'), clinesplit[1]);
                                }
                            }

                        }
                    }
                }

                int d = 100 * ++a / countMsg;

                if (d != pr)
                {
                    Console.WriteLine(d+"%");
                    pr = d;
                }

            }
        }

        static void CheckOtherResults(string pid, string[] Tests, string test)
        {
            foreach (String line in listmsg)
            {
                Dictionary<String, List<String[]>> msg = GetPackages(line);
                string[] pidS = msg["PID"][0];

                List<String[]> obxS = msg["OBX"];
                String logobx = "";

                if (pidS[2] == pid)
                {
                        for (int i = 0; i < obxS.Count; i++)
                        {
                            string[] ObservationIdentifier = obxS[i][3].Split('^');
                            if (Tests.Contains(ObservationIdentifier[0]))
                            {
                                String oi_1 = ObservationIdentifier[0];
                                String oi_4 = ObservationIdentifier[3];

                                String oi_3 = ObservationIdentifier[2];
                                String oi_6 = ObservationIdentifier[5];
                                ObservationIdentifier[0] = oi_4;
                                ObservationIdentifier[3] = oi_1;

                                ObservationIdentifier[2] = oi_6;
                                ObservationIdentifier[5] = oi_3;
                                String oi_result = "";
                                for (int oi = 0; oi < ObservationIdentifier.Count(); oi++)
                                {
                                    oi_result += ObservationIdentifier[oi] + "^";
                                }
                                obxS[i][3] = oi_result.Substring(0, oi_result.LastIndexOf('^'));
                               
                                String obxItem = "";
                                for (int b = 0; b < obxS[i].Count(); b++)
                                {
                                    obxItem += obxS[i][b] + "|";
                                }

                                logobx += obxItem.Substring(0, obxItem.LastIndexOf('|')) + Environment.NewLine;
                            }
                        }

                }

                if (logobx.Length > 0)
                {
                    String msgstr = msg["strMSH"][0][0] + Environment.NewLine + msg["strPID"][0][0] + Environment.NewLine + msg["strOBR"][0][0] + Environment.NewLine + msg["strZLR"][0][0] + Environment.NewLine + logobx ;

                    string[] address = pidS[11].Split(new char[] { '^' }, StringSplitOptions.RemoveEmptyEntries);
                    if (address.Length < 4)
                        errlog(msgstr);

                    log(msgstr);

                    Historylog(pidS[2] + "|" + test);
                }

            }
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
