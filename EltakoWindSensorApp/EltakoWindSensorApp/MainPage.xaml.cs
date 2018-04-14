// Copyright (c) Microsoft. All rights reserved.

using EltakoWindSensorApp.Utils;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Threading;
using Windows.Devices.Enumeration;
using Windows.Devices.Gpio;
using Windows.Devices.Spi;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.Web.Http;

namespace EltakoWindSensorApp
{
    public sealed partial class MainPage : Page
    {
        private const int LED_PIN = 6;
        private const int BUTTON_PIN = 5;
		private const string iot_homesecurity_url = "http://192.168.178.37/";
        private GpioPin ledPin;
        private GpioPin sensorPin;
        private GpioPinValue ledPinValue = GpioPinValue.High;
        private SolidColorBrush redBrush = new SolidColorBrush(Windows.UI.Colors.Red);
        private SolidColorBrush greenBrush = new SolidColorBrush(Windows.UI.Colors.Green);
        private Timer windsensorTimer;
		private double _windspeed = -1;
        private SpiDevice _mcp3008;
        private Timer temperatureTimer;
		private volatile int counter = 0;
		private MovingAverage _temperatureAverage;

		public MainPage()
        {
            InitializeComponent();
            InitGPIO();
            InitSpi();
        }



		private void InitGPIO()
        {
            var gpio = GpioController.GetDefault();

            // Show an error if there is no GPIO controller
            if (gpio == null)
            {
                WindStatus.Text = "There is no GPIO controller on this device.";
                return;
            }

            sensorPin = gpio.OpenPin(BUTTON_PIN);
            ledPin = gpio.OpenPin(LED_PIN);

            // Initialize LED to the OFF state by first writing a HIGH value
            // We write HIGH because the LED is wired in a active LOW configuration
            ledPin.Write(GpioPinValue.High);
            ledPin.SetDriveMode(GpioPinDriveMode.Output);

            // Check if input pull-up resistors are supported
            if (sensorPin.IsDriveModeSupported(GpioPinDriveMode.InputPullDown))
                sensorPin.SetDriveMode(GpioPinDriveMode.InputPullUp);
            else
                sensorPin.SetDriveMode(GpioPinDriveMode.Input);

            // Set a debounce timeout to filter out switch bounce noise from a button press
            sensorPin.DebounceTimeout = TimeSpan.FromMilliseconds(10);

            // Register for the ValueChanged event so our buttonPin_ValueChanged 
            // function is called when the button is pressed
            sensorPin.ValueChanged += WindsensorTriggered;

            WindStatus.Text = "GPIO pins initialized correctly.";

            // calculate windspeed every second
            windsensorTimer = new Timer(WindsensorTimerCallbackAsync, null, 1000, 1000);

        }

        private async void WindsensorTimerCallbackAsync(object state) // this timer is called every 1000ms
        {
            var current = counter*2; // two signals per round
            counter = 0;
            // formula from: http://zieren.de/ip-anemometer/
            var windspeed = 1.761 / (1 + current) + 3.013 * current; 
            if (windspeed<=1.761)
            {
                windspeed = 0;
            }
            Debug.WriteLine("Wind speed " + windspeed + " km/h");
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                WindStatus.Text = "Wind speed " + windspeed + " km/h";
            });
			if (windspeed != _windspeed)
			{
				try
				{
					_windspeed = windspeed;
					var message = new Message { Key = "Wind", Value = windspeed.ToString("F2") };
					var httpClient = new HttpClient();
					httpClient.DefaultRequestHeaders.Authorization = new Windows.Web.Http.Headers.HttpCredentialsHeaderValue("Basic", Base64.EncodeTo64("user:pass"));
					var content = new HttpStringContent(JsonConvert.SerializeObject(message), Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json");
					var result = await httpClient.PostAsync(new Uri(iot_homesecurity_url+ "api/queue/windsensor"), content);
					Debug.WriteLine("http status wind sensor call: " + result.StatusCode + " " + (int)result.StatusCode);
				}
				catch (Exception ex)
				{
					Debug.WriteLine(ex.Message);
				}
			}
		}

        private void WindsensorTriggered(GpioPin sender, GpioPinValueChangedEventArgs e)
        {
            // toggle the wind sensor rpm counter
            if (e.Edge == GpioPinEdge.FallingEdge)
            {
                counter++;
            }
        }

        private async void InitSpi()
        {
            //using SPI0 on the Pi
            var spiSettings = new SpiConnectionSettings(0);//for spi bus index 0
            spiSettings.ClockFrequency = 3600000; //3.6 MHz
            spiSettings.Mode = SpiMode.Mode0;

            string spiQuery = SpiDevice.GetDeviceSelector("SPI0");
            //using Windows.Devices.Enumeration;
            var deviceInfo = await DeviceInformation.FindAllAsync(spiQuery);
            if (deviceInfo != null && deviceInfo.Count > 0)
            {
                _mcp3008 = await SpiDevice.FromIdAsync(deviceInfo[0].Id, spiSettings);
				_temperatureAverage = new MovingAverage(10);
                // read temperature every second
                temperatureTimer = new Timer(TemperatureTimerCallback, null, 0, 60*1000);
            }
            else
            {
                TemperatureStatus.Text = "SPI Device Not Found :-(";
            }
        }

        private async void TemperatureTimerCallback(object state)
        {
            //From data sheet -- 1 byte selector for channel 0 on the ADC
            // First Byte sends the Start bit for SPI
            // Second Byte is the Configuration Byte
            //1 - single ended (this is where the 8 below is added)
            //0 - d2
            //0 - d1
            //0 - d0
            //             S321XXXX <-- single-ended channel selection configure bits
            // Channel 0 = 10000000 = 0x80 OR (8+channel) << 4
            // Third Byte is empty
            var transmitBuffer = new byte[3] { 1, 0x80, 0x00 };
            var receiveBuffer = new byte[3];

            _mcp3008.TransferFullDuplex(transmitBuffer, receiveBuffer);
            //first byte returned is 0 (00000000), 
            //second byte returned we are only interested in the last 2 bits 00000011 ( &3) 
            //shift 8 bits to make room for the data from the 3rd byte (makes 10 bits total)
            //third byte, need all bits, simply add it to the above result 
            var result = ((receiveBuffer[1] & 3) << 8) + receiveBuffer[2];
            //LM35 == 10mV/1degC ... 3.3V = 3300.0, 10 bit chip # steps is 2 exp 10 == 1024
            var mv = result * (5000.0 / 1024.0);
            var tempC = (mv - 500) / 10.0;
            var tempF = (tempC * 9.0 / 5.0) + 32;

			_temperatureAverage.Add(tempC);

            var output = "The temperature is " + tempC + " Celsius\nand " + tempF + " Farenheit";
			Debug.WriteLine(output);
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                TemperatureStatus.Text = output;
				AvgTemperatureStatus.Text = "Average temperature is " + _temperatureAverage.Average + " Celsius";
            });

			try
			{
				var message = new Message { Key = "Temperature", Value = _temperatureAverage.Average.ToString("F2") };
				var httpClient = new HttpClient();
				httpClient.DefaultRequestHeaders.Authorization = new Windows.Web.Http.Headers.HttpCredentialsHeaderValue("Basic", Base64.EncodeTo64("user:pass"));
				var content = new HttpStringContent(JsonConvert.SerializeObject(message), Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json");
				var postresult = await httpClient.PostAsync(new Uri(iot_homesecurity_url+"api/queue/windsensor"), content);
				Debug.WriteLine("http status temperature sensor call: " + postresult.StatusCode + " " + (int)postresult.StatusCode);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message);
			}
		}

    }
}