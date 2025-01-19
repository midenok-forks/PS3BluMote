using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using InTheHand.Net.Bluetooth.Sdp;

namespace PS3BluMote
{
    public static class BTUtils
    {
        public static List<string> GetNearbyRemoteAddresses(TimeSpan timeout)
        {
            List<string> result = new List<string>();
            try
            {
                BluetoothClient cli = new BluetoothClient();
                if (timeout != TimeSpan.Zero) cli.InquiryLength = timeout;
                BluetoothDeviceInfo[] devs = cli.DiscoverDevices().ToArray();
                foreach (BluetoothDeviceInfo dev in devs)
                {
                    if (dev.DeviceName.ToLower().Contains("bd remote control"))
                    {
                        if (RemoteBtState(null, dev) == RemoteBtStates.Awake)
                        {
                            string candidate = FormatBtAddress(null, dev.DeviceAddress, "N");
                            if (!result.Contains(candidate)) result.Add(candidate);
                        }
                    }
                }
            }
            catch
            {
                DebugLog.write("BTUtils.GetNearbyRemoteAddresses Failed");
            }
            return result;
        }
        public static RemoteBtStates RemoteBtState(string btAddress, BluetoothDeviceInfo dev)
        {
            RemoteBtStates result = RemoteBtStates.Unknown;
            BluetoothDeviceInfo device = null;
            if (dev == null && btAddress == null) return result;
            else if (dev == null)
            {
                try { device = new BluetoothDeviceInfo(BluetoothAddress.Parse(btAddress)); }
                catch
                {
                    DebugLog.write("BTUtils.RemoteBtState failed while creating BluetoothDeviceInfo");
                }
            }
            else device = dev;

            if (device != null)
            {
                try
                {
                    var services = device.GetRfcommServicesAsync(false);
                    result = RemoteBtStates.Awake;
                }
                catch
                {
                    result = RemoteBtStates.Hibernated;
                }
            }
            if (result == RemoteBtStates.Unknown) DebugLog.write("BTUtils.RemoteBtState returns Unknown");
            else if (result == RemoteBtStates.Awake) DebugLog.write("BTUtils.RemoteBtState returns Awake");
            else if (result == RemoteBtStates.Hibernated) DebugLog.write("BTUtils.RemoteBtState returns Hibernated");
            return result;
        }
        public static void SetHIDServiceState(bool state, string btAddress, BluetoothDeviceInfo dev)
        {
            try
            {
                BluetoothDeviceInfo device;
                if (dev == null && btAddress == null) return;
                else if (dev == null) device = new BluetoothDeviceInfo(BluetoothAddress.Parse(btAddress));
                else device = dev;

                if (state) DebugLog.write("Reconnecting HID Service from the Remote...");
                else DebugLog.write("Disconnecting HID Service from the Remote...");

                device.SetServiceState(BluetoothService.HumanInterfaceDevice, state);

                if (state) DebugLog.write("HID Service reconnected...");
                else DebugLog.write("HID Service disconnected...");
            }
            catch
            {
                DebugLog.write("BTUtils.SetHIDServiceState failed");
            }
        }
        public static void HibernatePS3Remote(bool checkBefore, string btAddress, BluetoothDeviceInfo dev)
        {
            BluetoothDeviceInfo device;
            if (dev == null && btAddress != null)
            {
                string addr = FormatBtAddress(btAddress, null, "N");
                if (addr.Length > 0) device = new BluetoothDeviceInfo(BluetoothAddress.Parse(btAddress));
                else device = null;
            }
            else if (dev != null) device = dev;
            else device = null;

            if (device != null)
            {
                if ((checkBefore && RemoteBtState(null, device) == RemoteBtStates.Awake) || !checkBefore)
                {
                    SetHIDServiceState(false, btAddress, device);
                    SetHIDServiceState(true, btAddress, device);
                }
            }
            else
            {
                DebugLog.write("BTUtils.HibernatePS3Remote can't create BluetoothDeviceInfo");
            }
        }
        public static string FormatBtAddress(string s, BluetoothAddress btA, string format)
        {
            if (s != null && (s.Length == 12 || s.Length == 17))
            {
                try
                {
                    return BluetoothAddress.Parse(s).ToString(format).ToLower();
                }
                catch
                {
                    DebugLog.write("BTUtils.BtAddress Failed on " + s);
                    return "";
                }
            }
            else if (btA != null)
            {
                try
                {
                    return btA.ToString(format).ToLower();
                }
                catch
                {
                    DebugLog.write("BTUtils.BtAddress Failed with unknown btA");
                    return "";
                }
            }
            else
            {
                DebugLog.write("BTUtils.BtAddress : Invalid Parameters");
                return "";
            } 
        }

    }
    public enum RemoteBtStates { Awake, Hibernated, Unknown }
}
