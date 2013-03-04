using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace PS3BluMote
{
    public static class FindBtAddress
    {
        public static List<string> Find(string pid, string vid)
        {
            List<string> result = new List<string>();

            List<string> regResult = FromReg(pid,vid);
            if (regResult.Count == 1)
            {
                result.Add(regResult[0]);
                DebugLog.write("FindBtAddress.Find returns regResult since we got only one match");
            }
            else
            {
                DebugLog.write("FindBtAddress.Find will do a BT Search");
                List<string> btResult = FromBT();

                if (btResult.Count == 0) return regResult;
                else if (regResult.Count > 0)
                {
                    foreach (string res in regResult)
                    {
                        if (btResult.Contains(res))
                        {
                            if (!result.Contains(res)) result.Add(res);
                        }
                    }
                }
                else
                {
                    foreach (string res in btResult)
                    {
                        result.Add(res);
                    }
                }
            }
            return result;
            
        }

        private static List<string> FromBT()
        {
            List<string> btResult;
            btResult = BTUtils.GetNearbyRemoteAddresses(TimeSpan.FromSeconds(1));
            if (btResult.Count == 0) btResult = BTUtils.GetNearbyRemoteAddresses(TimeSpan.Zero);
            for (int i = 0; i < btResult.Count; i++)
            {
                btResult[i] = BTUtils.FormatBtAddress(btResult[i], null, "N");
                DebugLog.write("FindBTAddress.FromBT will return " + btResult[i]);
            }
            return btResult;
        }
        private static List<string> FromReg(string pid, string vid)
        {
            List<string> result = new List<string>();
            List<RegistryKey> regResult;

            try
            {
                string regFilter;
                if (vid.ToLower().Contains("x")) regFilter = vid.Substring(2);
                else regFilter = vid;
                if (pid.ToLower().Contains("x")) regFilter += "_PID&" + pid.Substring(2);
                else regFilter += "_PID&" + pid;

                regResult = RegUtils.GetKeys(Registry.LocalMachine, regFilter, "Bluetooth_UniqueID");
                foreach (RegistryKey k in regResult)
                {
                    try
                    {
                        string v = (string)k.GetValue("Bluetooth_UniqueID");
                        if (v.Length != 0 && v.Contains("#") && v.Contains("_") && (v.IndexOf("_") - v.IndexOf("#")) == 13)
                        {
                            v = v.Substring(v.IndexOf("#") + 1, 12);
                            v = BTUtils.FormatBtAddress(v, null,"N");
                            if (v != "")
                            {
                                if (!result.Contains(v))
                                {
                                    DebugLog.write("FindBTAddress.FromReg will return " + v);
                                    result.Add(v);
                                }
                            }
                            else
                            {
                                DebugLog.write("FindBTAddress.FromReg parsing returned the empty String (" + (string)k.GetValue("Bluetooth_UniqueID") + ")");
                            }
                        }
                    }
                    catch
                    {
                        try
                        {
                            DebugLog.write("FindBTAddress.FromReg Failed while parsing" + k.Name);
                        }
                        catch
                        {
                            DebugLog.write("FindBTAddress.FromReg Failed while parsing some key ..");
                        }
                    }
                }
                return result;
            }
            catch
            {
                DebugLog.write("FindBTAddress.FromReg Failed");
                return result;
            }

        }
    }
}
