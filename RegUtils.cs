using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace PS3BluMote
{
    public static class RegUtils
    {
        private static List<RegistryKey> FindResult = new List<RegistryKey>();

        private static void Find(RegistryKey key, string skFilter, string keyName)
        {
            if (key.Name.Contains(skFilter))
            {
                foreach (string vn in key.GetValueNames())
                {
                    if (vn == keyName)
                    {
                        FindResult.Add(key);
                        break;
                    }
                }
            }
            foreach (string skn in key.GetSubKeyNames())
            {
                try
                {
                    Find(key.OpenSubKey(skn), skFilter, keyName);
                }
                catch { }
            }
        }

        public static List<RegistryKey> GetKeys(RegistryKey key, string skFilter, string keyName)
        {
            List<RegistryKey> result = new List<RegistryKey>();
            FindResult.Clear();
            Find(key, skFilter, keyName);

            foreach (RegistryKey rk in FindResult) result.Add(rk);

            FindResult.Clear();
            
            return result;

        }
        public static string GetDevConnectedSound()
        {
            try
            {
                return (string)Registry.GetValue(@"HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\DeviceConnect\.Current", "", "");
            }
            catch
            {
                DebugLog.write("RegUtils.GetDevConnectedSound() Failed");
                return null;
            }
        }
        public static string GetDevDisconnectedSound()
        {
            try
            {
                return (string)Registry.GetValue(@"HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\DeviceDisconnect\.Current", "", "");
            }
            catch
            {
                DebugLog.write("RegUtils.GetDevConnectedSound Failed");
                return null;
            }
        }
        public static void SetDevConnectedSound(string sound)
        {
            try
            {
                Registry.SetValue(@"HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\DeviceConnect\.Current", "", sound);
            }
            catch 
            {
                DebugLog.write("RegUtils.SetDevConnectedSound Failed");
            }
        }
        public static void SetDevDisconnectedSound(string sound)
        {
            try
            {
                Registry.SetValue(@"HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\DeviceDisconnect\.Current", "", sound);
            }
            catch
            {
                DebugLog.write("RegUtils.SetDevDisconnectedSound Failed");
            }
        }
    }
}
