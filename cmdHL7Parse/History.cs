using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace cmdHL7Parse
{
    public class H_MSG
    {
        public string MSH_Block { get; set; }
        public string pid_id { get; set; }
        public string obx_id { get; set; }
        public string abnormal_flag { get; set; }
        public bool delete { get; set; }  

    }
    class History
    {
        public static List<H_MSG> ParseMSG(String FileName)
        {
            var msg_list = new List<H_MSG>();

            if (File.Exists(FileName))
            {
                var MSG_lines = HL7.GetBlock(File.ReadAllText(FileName), "MSH");

                foreach (var item in MSG_lines)
                {
                    var _msg = new H_MSG();
                    _msg.MSH_Block = item;
                    _msg.delete = false;
                    using (StringReader reader = new StringReader(item))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            string[] lineSplit = line.Split('|');

                            if (lineSplit[0] == "PID")
                            {
                                _msg.pid_id = lineSplit[2];
                            }
                            else if (lineSplit[0] == "OBX")
                            {
                                _msg.obx_id = lineSplit[3].Split('^')[3];
                                _msg.abnormal_flag = lineSplit[8];
                            }
                        }
                    }

                    msg_list.Add(_msg);
                }
            }

            return msg_list;
        }
    }
}