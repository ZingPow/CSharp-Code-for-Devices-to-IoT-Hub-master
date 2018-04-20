using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Threading;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using RPDevice.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Devices.Gpio;
using Windows.Devices.Spi;
using Windows.UI.Xaml;

namespace RPDevice.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private const string IotHubUri = "calgary1.azure-devices.net";
        private const string DeviceKey = "Rt07J91KugmGZGAqfa+lMYYpC8MoYoeIdKTkjM/5Gts=";
        private const string DeviceId = "RealDevice";
        private static DeviceClient _deviceClient;

        private const int LED_PIN = 4;
        private static GpioPin ledPin;
        private static GpioPinValue ledPinValue = GpioPinValue.Low;

        private const string SPI_CONTROLLER_NAME = "SPI0";
        private const Int32 SPI_CHIP_SELECT_LINE = 0;
        private SpiDevice SpiADC;

        private readonly byte StartByte = 0x01;
        private readonly byte Channel0 = 0x80;

        double Longitude;
        double Latitude;
        private Geolocator _geolocator;

        private int adcTemp;

        private DispatcherTimer periodicTimer;

        [DataContract]
        internal class TelemetryData
        {
            [DataMember]
            internal string Time;

            [DataMember]
            internal string DeviceId;

            [DataMember]
            internal double Temperature;

            [DataMember]
            internal double Latitude;

            [DataMember]
            internal double Longitude;

            [DataMember]
            internal int LEDStatus;
        }

        class LEDStatusCommand
        {
            [DataMember]
            public string time { get; set; }

            [DataMember]
            public int ledstatus { get; set; }
        }

        private const int MAXRECORDS = 120;
        public ObservableCollection<ConfigData> DataReadings { set; get; }
        public ObservableCollection<string> Statuses { set; get; }

        public MainViewModel()
        {
            DispatcherHelper.Initialize();

            DataReadings = new ObservableCollection<ConfigData>();
            Statuses = new ObservableCollection<string>();

            InitAll();

            IoTHubInit();

            LocateDevice();

            /* Now that everything is initialized, create a timer so we read data every 500mS */
            periodicTimer = new DispatcherTimer();
            periodicTimer.Tick += PeriodicTimer_Tick;
            periodicTimer.Interval = new TimeSpan(0, 0, 15);
            periodicTimer.Start();
        }

        private void PeriodicTimer_Tick(object sender, object e)
        {
            ReadADC();
        }

        private async Task IoTHubInit()
        {
            try
            {
                _deviceClient = DeviceClient.Create(IotHubUri, new DeviceAuthenticationWithRegistrySymmetricKey(DeviceId, DeviceKey), Microsoft.Azure.Devices.Client.TransportType.Mqtt);

                if (_deviceClient != null)
                {
                    await _deviceClient.OpenAsync();

                    Twin MyTwin = await _deviceClient.GetTwinAsync();

                    if (MyTwin != null && MyTwin.Properties.Desired.Contains("LEDStatus"))
                    {
                        DisplayStatus("Initializing LED to Device Twin Setting");
                        SetLED((int)MyTwin.Properties.Desired["LEDStatus"]);
                    }

                    _deviceClient.SetConnectionStatusChangesHandler(ConnectionStatusChangeHandler);

                    _deviceClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, null).Wait();

                    // setup callbacks for direct methods
                    await _deviceClient.SetMethodHandlerAsync("DMTurnOnLED", DMTurnOnLED, null);
                    await _deviceClient.SetMethodHandlerAsync("DMTurnOffLED", DMTurnOffLED, null);

                    // recieve one way messages
                    await ReceiveCloudToDeviceMessageAsync();

                    UpdateTwin(0);
                }
            }
            catch (Exception e)
            {
                DisplayStatus(e.Message);
            }
        }

        private void ConnectionStatusChangeHandler(ConnectionStatus status, ConnectionStatusChangeReason reason)
        {
            DisplayStatus(string.Format("Connection Status Changed to {0} change reason {1}", status, reason));
        }

        private void UpdateLED(int led)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                LED = led;
            });
        }

        private void UpdateChart(ConfigData data)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                //trim extra data
                while (DataReadings.Count > MAXRECORDS)
                {
                    DataReadings.RemoveAt(0);
                }

                DataReadings.Add(data);

            });
        }

        private void DisplayStatus(string msg)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() =>
            {
                if (Statuses.Count > 5)
                {
                    Statuses.RemoveAt(0);
                }
                Statuses.Add(string.Format("{0} - {1}", DateTime.Now.ToString("g"), msg));
            });
        }

        private async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            if (desiredProperties.Contains("LEDStatus"))
            {
                SetLED((int)desiredProperties["LEDStatus"]);
                DisplayStatus("Recieved Property Changed for LED");
            }
        }

        private void SetLED(int ledstatus)
        {
            if (ledstatus != 1)
            {

                TurnOffLED();
            }
            else
            {
                TurnOnLED();
            }
        }

        private async void InitAll()
        {
            var gpio = GpioController.GetDefault();

            if (gpio == null)
            {
                DisplayStatus("There is no GPIO controller on this device.");
                return;
            }

            ledPin = gpio.OpenPin(LED_PIN);
            ledPin.SetDriveMode(GpioPinDriveMode.Output);
            ledPin.Write(ledPinValue);

            try
            {
                await InitSPI();    /* Initialize the SPI bus for communicating with the ADC      */
            }
            catch (Exception ex)
            {
                DisplayStatus(ex.Message);
                return;
            }
            DisplayStatus("Running");
        }

        private async Task LocateDevice()
        {
            try
            {
                var accessStatus = await Geolocator.RequestAccessAsync();

                switch (accessStatus)
                {
                    case GeolocationAccessStatus.Allowed:
                        // Create Geolocator and define perodic-based tracking (2 minute interval).
                        _geolocator = new Geolocator { MovementThreshold = 100 };

                        // Subscribe to the PositionChanged event to get location updates.
                        _geolocator.PositionChanged += OnPositionChanged;

                        // Subscribe to StatusChanged event to get updates of location status changes.
                        _geolocator.StatusChanged += OnStatusChanged;
                        break;

                    case GeolocationAccessStatus.Denied:
                        DisplayStatus("Access to location is denied.");
                        break;

                    case GeolocationAccessStatus.Unspecified:
                        DisplayStatus("Location Unspecificed error!");
                        break;
                }
            }
            catch (TaskCanceledException)
            {
                DisplayStatus("Location Canceled.");
            }
            catch (Exception ex)
            {
                DisplayStatus(ex.Message);
            }
        }

        private void OnPositionChanged(Geolocator sender, PositionChangedEventArgs e)
        {
            UpdateLocationData(e.Position);
        }


        private void OnStatusChanged(Geolocator sender, StatusChangedEventArgs e)
        {
            switch (e.Status)
            {
                case PositionStatus.Ready:
                    // Location platform is providing valid data.
                    break;
                case PositionStatus.Initializing:
                    // Location platform is attempting to acquire a fix. 
                    DisplayStatus("Location platform is attempting to obtain a position.");
                    break;
                case PositionStatus.NoData:
                    // Location platform could not obtain location data.
                    DisplayStatus("Not able to determine the location.");
                    break;
                case PositionStatus.Disabled:
                    // The permission to access location data is denied by the user or other policies.
                    Status = "Access to location is denied.";
                    // Clear cached location data if any
                    UpdateLocationData(null);
                    break;
                case PositionStatus.NotInitialized:
                    // The location platform is not initialized. This indicates that the application 
                    // has not made a request for location data.
                    DisplayStatus("No request for location is made yet.");
                    break;
                case PositionStatus.NotAvailable:
                    // The location platform is not available on this version of the OS.
                    DisplayStatus("Location is not available on this version of the OS.");
                    break;
                default:
                    //Status = String.Empty;
                    break;
            }
        }

        private async void UpdateLocationData(Geoposition position)
        {
            if (position == null)
            {
                Latitude = 0;
                Longitude = 0;
            }
            else
            {
                Latitude = position.Coordinate.Point.Position.Latitude;
                Longitude = position.Coordinate.Point.Position.Longitude;
                DisplayStatus(string.Format("Update Location Lat {0}, Long {1}, Source {2}", Latitude, Longitude, position.Coordinate.PositionSource.ToString()));
                TwinCollection reportedProperties = new TwinCollection();
                reportedProperties["Latitude"] = Latitude;
                reportedProperties["Longitude"] = Longitude;
                reportedProperties["LocationSource"] = position.Coordinate.PositionSource.ToString();
                reportedProperties["LocationAccuracy"] = position.Coordinate.Accuracy;
                await _deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
            }
        }

        private void TurnOnLED()
        {
            ledPinValue = GpioPinValue.High;
            ledPin.Write(ledPinValue);
            UpdateLED(1);
            DisplayStatus("LED Turned ON");
            ConfigData update = new ConfigData
            {
                ReadingDateTime = DateTime.Now,
                Temperature = CurrentTemp,
                LED = 1
            };

            UpdateChart(update);

            UpdateTwin(LED);
        }

        private void TurnOffLED()
        {
            ledPinValue = GpioPinValue.Low;
            ledPin.Write(ledPinValue);
            UpdateLED(0);
            DisplayStatus("LED Turned OFF");
            ConfigData update = new ConfigData
            {
                ReadingDateTime = DateTime.Now,
                Temperature = CurrentTemp,
                LED = 0
            };

            UpdateChart(update);

            UpdateTwin(LED);
        }



        private async void UpdateTwin(int led)
        {
            TwinCollection reportedProperties = new TwinCollection();
            reportedProperties["LEDStatus"] = led;
            await _deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
            DisplayStatus("Device Twin LED Updated");
        }


        private async Task InitSPI()
        {
            try
            {
                var spiSettings = new SpiConnectionSettings(SPI_CHIP_SELECT_LINE); //we are using line 0
                spiSettings.ClockFrequency = 500000;   /* 0.5MHz clock rate                                        */
                spiSettings.Mode = SpiMode.Mode0;      /* The ADC expects idle-low clock polarity so we use Mode0  */

                var controller = await SpiController.GetDefaultAsync();
                SpiADC = controller.GetDevice(spiSettings);
            }

            /* If initialization fails, display the exception and stop running */
            catch (Exception ex)
            {
                throw new Exception("SPI Initialization Failed", ex);
            }
        }

        private void InitGpio()
        {
            var gpio = GpioController.GetDefault();

            if (gpio == null)
            {
                throw new Exception("There is no GPIO controller on this device");
            }
        }

        public async void ReadADC()
        {
            ConfigData update = new ConfigData();
            update.ReadingDateTime = DateTime.Now;

            byte[] readBuffer = new byte[3]; /* Buffer to hold read data*/
            byte[] writeBuffer = new byte[3] { StartByte, Channel0, 0x00 };

            //Get Temperature
            readBuffer = new byte[3]; /* Buffer to hold read data*/
            writeBuffer = new byte[3] { StartByte, Channel0, 0x00 };

            SpiADC.TransferFullDuplex(writeBuffer, readBuffer); /* Read data from the ADC                           */
            adcTemp = convertToInt(readBuffer);                /* Convert the returned bytes into an integer value */
            // millivolts = value * (volts in millivolts / ADC steps)
            double millivolts = adcTemp * (3300.0 / 1024.0);
            CurrentTemp = CurrentTemp = (millivolts - 500) / 10.0; //given equation from sensor documentation
            LED = (ledPinValue == GpioPinValue.Low) ? 0 : 1;

            //send data to Azure
            SendDataToCloudAsync(CurrentTemp, LED);

            update.Temperature = CurrentTemp;
            update.LED = LED;

            UpdateChart(update);

        }

        public async Task SendDataToCloudAsync(double temp, int ledstatus)
        {
            try
            {
                TelemetryData telemetryDataPoint = new TelemetryData()
                {
                    DeviceId = DeviceId,
                    Time = DateTime.UtcNow.ToString("o"),
                    Temperature = CurrentTemp,
                    Latitude = Latitude,
                    Longitude = Longitude,
                    LEDStatus = ledstatus
                };


                var messageString = JsonConvert.SerializeObject(telemetryDataPoint);
                var message = new Microsoft.Azure.Devices.Client.Message(Encoding.UTF8.GetBytes(messageString));
                if (_deviceClient != null)
                {
                    await _deviceClient.SendEventAsync(message);
                }
                Debug.WriteLine("{0} > Sending message: {1}", DateTime.Now, messageString);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public async Task ReceiveCloudToDeviceMessageAsync()
        {
            while (true)
            {
                Microsoft.Azure.Devices.Client.Message receivedMessage = await _deviceClient.ReceiveAsync();
                if (receivedMessage == null) continue;

                try
                {
                    string jsonString = Encoding.UTF8.GetString(receivedMessage.GetBytes());
                    LEDStatusCommand ledStatusCommand = JsonConvert.DeserializeObject<LEDStatusCommand>(jsonString);

                    DisplayStatus("Received One Way Notification " + jsonString);

                    if (ledStatusCommand.ledstatus == 1)
                    {
                        TurnOnLED();
                    }
                    else
                    {
                        TurnOffLED();
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }

                // We received the message, indicate IoTHub we are finished with it
                await _deviceClient.CompleteAsync(receivedMessage);
            }
        }

        Task<MethodResponse> DMTurnOnLED(MethodRequest methodRequest, object userContext)
        {
            DisplayStatus("Received Direct Message LED ON");
            TurnOnLED();
            string result = "{\"LED\":\"On\"}";
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }

        Task<MethodResponse> DMTurnOffLED(MethodRequest methodRequest, object userContext)
        {
            DisplayStatus("Received Direct Message LED OFF");
            TurnOffLED();
            string result = "{\"LED\":\"Off\"}";
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }

        /* Convert the raw ADC bytes to an integer for MCP3008 */
        private int convertToInt(byte[] data)
        {
            int result = 0;
            //bit bashing is inevitable when you play at this level 
            result = data[1] & 0x03;
            result <<= 8;
            result += data[2];

            return result;
        }

        private double _currentTemp;
        public double CurrentTemp
        {
            get
            {
                return _currentTemp;
            }
            set
            {
                Set(ref _currentTemp, value);
            }
        }

        private int _led;
        public int LED
        {
            get
            {
                return _led;
            }
            set
            {
                Set(ref _led, value);
            }
        }

        private string _status;

        public string Status
        {
            get
            {
                return _status;
            }
            set
            {
                Set(ref _status, value);
            }
        }
    }
}
