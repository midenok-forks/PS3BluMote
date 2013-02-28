using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace BDRemoteSleep
{
    class SearchKey
    {
        public RegistryKey Result;
        private RegistryKey _base;
        public SearchKey(RegistryKey key, string skFilter, string keyName)
        {
            _base = key;
            Result = null;
            Find(key, skFilter, keyName);
        }
        public void Find(RegistryKey key, string skFilter, string keyName)
        {
            if (key.Name.Contains(skFilter))
            {
                foreach (string vn in key.GetValueNames())
                {
                    if (vn == keyName)
                    {
                        Result = key;
                        break;
                    }
                }
            }
            if (Result == null)
            {
                foreach (string skn in key.GetSubKeyNames())
                {
                    if (Result == null)
                    {
                        try
                        {
                            Find(key.OpenSubKey(skn), skFilter, keyName);
                        }
                        catch { }
                    }
                }
            }
        }
    }
}
