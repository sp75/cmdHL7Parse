using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace cmdHL7Parse
{
    public class MSG
    {
        public string MSH_line { get; set; }
        public string[] MSH_split { get; set; }
        public string PID_line { get; set; }
        public string[] PID_split { get; set; }
        public string pid_id { get; set; }
        public int pid_year { get; set; }
        public List<OBR> OBRs { get; set; }
        public bool bad_pid_name { get; set; }
        public MSG()
        {
            OBRs = new List<OBR>();
        }
    }

    public class OBR
    {
        public string OBR_line { get; set; }
        public string[] OBR_split { get; set; }
        public List<string> NTE_list { get; set; }
        public List<ZLR> ZLRs { get; set; }
        public OBR()
        {
            ZLRs = new List<ZLR>();
            NTE_list = new List<string>();
        }
    }

    public class ZLR
    {
        public String ZLR_heder { get; set; }
        public string[] ZLR_split { get; set; }
        public List<OBX> OBXs { get; set; }
       
        public ZLR()
        {
            OBXs = new List<OBX>();
        }
    }

    public class OBX
    {
        public String OBX_heder { get; set; }
        public string[] OBX_split_heder { get; set; }
        public string abnormal_flag { get; set; }
        public string obx_id { get; set; }
    }

    public static class HL7
    {
        public static List<MSG>  ParseMSG(String FileName)
        {
            var msg_list = new List<MSG>();

            if (File.Exists(FileName))
            {
                var MSG_lines = GetBlock(File.ReadAllText(FileName), "MSH");

                foreach (var MSG in MSG_lines)
                {
                //    var _msg = new MSG();
                    var _msg = GetMSH(MSG);

                    msg_list.Add(_msg);
                }
            }

            return msg_list;
        }

        private static MSG GetMSH(string MSG)
        {
            MSG _msg = new MSG();
            _msg.bad_pid_name = true;

            using (StringReader reader = new StringReader(MSG))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] lineSplit = line.Split('|');

                    if (lineSplit[0] == "MSH")
                    {
                        _msg.MSH_line = line;
                        _msg.MSH_split = lineSplit;

                    }
                    else if (lineSplit[0] == "PID")
                    {
                        string[] p_name = lineSplit[ 5 ].Split( '^' );

                        if (p_name.Count() >= 2)
                        {
                            String f_name = p_name[0].Trim();
                            String l_name = p_name[1].Trim();
                            var r = new Regex(@"\d+"); // если в имени нет числа то обрабатываем
                            if (!r.IsMatch(f_name) && !r.IsMatch(l_name) && !f_name.ToLower().Contains("proficiency") &&
                                 !l_name.ToLower().Contains("proficiency"))
                            {
                                _msg.bad_pid_name = false;
                            }
                        }
 
                        lineSplit[10] = "U";  //1. PID10.1 - put letter U
                        lineSplit[22] = "U";  //2. PID22.1 - put letter U

                        _msg.PID_split = lineSplit;
                        _msg.PID_line = String.Join("|", lineSplit);
                        _msg.pid_id = lineSplit[2];
                        _msg.pid_year = !String.IsNullOrEmpty(lineSplit[7]) ? CalculateAge(DateTime.ParseExact(lineSplit[7], "yyyyMMdd", CultureInfo.InvariantCulture), DateTime.Now) : 150;
                    }
                }

                var OBR_lines = GetBlock(MSG, "OBR");
                foreach (var OBR in OBR_lines)
                {
                    _msg.OBRs.Add(GetOBR(OBR));
                }

            }
            return _msg;
        }

        private static OBR GetOBR(string OBR_block)
        {
            using (StringReader reader = new StringReader(OBR_block))
            {
                var _obr = new OBR();

                string line; // Собираем инфо о NTE и читаем заголовок OBR
                while ((line = reader.ReadLine()) != null)
                {
                    string[] lineSplit = line.Split('|');

                    if (lineSplit[0] == "OBR")
                    {
                        lineSplit[22] = lineSplit[14]; //3. OBR22.1 - put the same value as in OBR14.1

                        _obr.OBR_line = String.Join( "|", lineSplit );
                        _obr.OBR_split = lineSplit;
                    }
                    else if (lineSplit[0] == "NTE")
                    {
                        _obr.NTE_list.Add(line);
                    }
                }

                _obr.ZLRs = new List<ZLR>();
                var ZLR_lines = GetBlock(OBR_block, "ZLR");

                foreach (var ZLR_item in ZLR_lines)
                {
                    var _zlr = new ZLR();
                    _zlr.ZLR_heder = new StringReader(ZLR_item).ReadLine();
                    _zlr.ZLR_split = _zlr.ZLR_heder.Split('|');

                    _zlr.ZLR_split[1] = _zlr.ZLR_split[3]; //4. ZLR1.1 - put the same value as ZLR3.1
                                                           //5. ZLR1.3 - put the same value as ZLR3.3
                                                           //6. ZLR1.4 - put the same value as ZLR3.4
                                                           //7. ZLR1.5 - put the same value as ZLR3.5 
                    _zlr.ZLR_heder = String.Join( "|", _zlr.ZLR_split );
  
                    var OBX_lines = GetBlock(ZLR_item, "OBX");
                    foreach (var OBX_item in OBX_lines)
                    {
                        var _obx = new OBX();
                        _obx.OBX_heder = new StringReader(OBX_item).ReadLine();
                        _obx.OBX_split_heder = _obx.OBX_heder.Split('|');
                        _obx.abnormal_flag = _obx.OBX_split_heder[8];
                        _obx.obx_id = _obx.OBX_split_heder[3].Split('^')[0];

                        _zlr.OBXs.Add(_obx);
                    }
                    _obr.ZLRs.Add(_zlr);

                }

                return _obr;
            }
        }

        public static List<String> GetBlock(String block, String teg)
        {
            List<String> list = new List<String>();
            String str = "";
            bool tr = false;

            using (StringReader reader = new StringReader(block))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] lineSplit = line.Split('|');

                    if (tr)
                    {
                        if (lineSplit[0] == teg)
                        {
                            tr = false;
                            list.Add(str);
                            str = "";
                        }
                        else str += line + Environment.NewLine;
                    }
                    if (lineSplit[0] == teg && !tr)
                    {
                        tr = true;
                        str += line + Environment.NewLine;
                    }
                }
            }

            if (str.Length > 0) list.Add(str);

            return list;
        }

        static int CalculateAge(DateTime birthDate, DateTime now)
        {
            int age = now.Year - birthDate.Year;
            if (now.Month < birthDate.Month || (now.Month == birthDate.Month && now.Day < birthDate.Day)) age--;
            return age;
        }

    }
}
