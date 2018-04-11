using System;
using System.Threading.Tasks;

using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Common.Exceptions;

namespace CreateDeviceIdentity
{
    class Program
    {
        static RegistryManager registryManager;
        static string connectionString = "HostName=calgary1.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=Access key here";
        static string deviceName = "mySimulatedDevice";

        static async Task Main(string[] args)
        {
            registryManager = RegistryManager.CreateFromConnectionString(connectionString);
            try
            {
                await AddDeviceAsync();
            }
            catch(Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
            Console.ReadLine();
        }
        private static async Task AddDeviceAsync()
        {
            string deviceId = deviceName;
            Device device;
            try
            {
                device = await registryManager.AddDeviceAsync(new Device(deviceId));
            }
            catch (DeviceAlreadyExistsException)
            {
                device = await registryManager.GetDeviceAsync(deviceId);
            }
            Console.WriteLine("Generated device key: {0}", device.Authentication.SymmetricKey.PrimaryKey);
        }
    }
}
