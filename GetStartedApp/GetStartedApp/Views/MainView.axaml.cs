using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HidSharp;
using RJCP.IO.Ports;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using SharpHook;

namespace GetStartedApp.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("logs/myapp.txt", rollingInterval: RollingInterval.Day)
            .WriteTo.Sink(new TextBoxSink(AppendLog))
            .CreateLogger();
        Log.Information("Application started");

        AddImpToUI();


        Refresh_Click(null, new RoutedEventArgs());
        ListenFornDevices();
        // HidSharp.DeviceList.Local.RaiseChanged();
    }

    private void AddImpToUI()
    {
        foreach (var item in Enum.GetValues(typeof(Implementation)))
        {
            ImplementationDisp.Items.Add(item);
        }
        ImplementationDisp.SelectedItem = imp;
    }

    private void ListenFornDevices()
    {
        // Plug-in event
        var insertWatcher = new ManagementEventWatcher();
        insertWatcher.EventArrived += InsertWatcher_EventArrived; ;
        insertWatcher.Query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2");
        insertWatcher.Start();

        //connect to device if allready plugged in
        InsertWatcher_EventArrived(null, null);
        // Unplug event
        var removeWatcher = new ManagementEventWatcher();
        removeWatcher.EventArrived += RemoveWatcher_EventArrived; ;
        removeWatcher.Query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 3");
        removeWatcher.Start();

        // Console.WriteLine("Monitoring USB device changes...");
        //  Console.ReadLine(); // Keep app running

        // Optional: Stop watchers on exit
        // insertWatcher.Stop();
        //  removeWatcher.Stop();
    }
    int vendorId = 0x303a; // Example VID
    int productId = 0x1001; // Example PID
    private void RemoveWatcher_EventArrived(object sender, EventArrivedEventArgs e)
    {
        Task.Delay(50).Wait();
        if (_hidStream is not null)
        {
            if (DeviceList.Local.GetHidDevices(vendorId, productId).FirstOrDefault(defaultValue: null) == null)
            {
                Log.Information("Disconnected to HID device");
                _hidStream.Dispose();
                _hidStream = null;
                Dispatcher.UIThread.Invoke(() => DeviceStatus.Text = "🔴 Disconnected");
            }
        }


    }

    HidStream? _hidStream = null;
    private void InsertWatcher_EventArrived(object sender, EventArrivedEventArgs e)
    {
        var deviceList = DeviceList.Local;
        Task.Delay(50).Wait();
        var device = deviceList.GetHidDevices(vendorId, productId).FirstOrDefault(defaultValue: null);
        if (device == null)
        {
            Log.Information("Inserted device is not what we want");
            return;
        }
        Log.Information("Connected to HID device");


        if (_hidStream is null)
        {
            _hidStream = device.Open();
        }
        else
            return;




        Dispatcher.UIThread.Invoke(() =>
             {
                 DeviceStatus.Text = "🟢 Connected";
             });

    }

    private void ImplementationDisp_SelectionChanged(object sender, RoutedEventArgs a)
    {
        imp = (Implementation)ImplementationDisp.SelectedItem;
    }




    SerialPort _serialPort = new SerialPort();


    private void Refresh_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Log.Information("Serial ports refreshed");
        DropDown.Items.Clear();
        //var serport = new SerialPort(DropDown.SelectedItem as string);
        foreach (var item in SerialPortStream.GetPortDescriptions())
        {
            DropDown.Items.Add(item);
        }


    }

    private void Connect_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_serialPort is null)
            _serialPort = new SerialPort();
        if (_serialPort.IsOpen)
            return;

        if (DropDown.SelectedItem is null)
        {
            Log.Warning("No port selected!");
            return;
        }
        _serialPort.PortName = ((PortDescription)DropDown.SelectedItem).Port;
        _serialPort.BaudRate = 115200;
        _serialPort.DataReceived += SerialPort_DataReceived;
        _serialPort.ErrorReceived += _serialPort_ErrorReceived;
        _serialPort.DtrEnable = true;
        _serialPort.RtsEnable = true;
        try
        {

            _serialPort.Open();
            Log.Information("Connected to serial port");
            //   _serialPort.DiscardOutBuffer();
            //   _serialPort.DiscardInBuffer();
        }
        catch (Exception exception)
        {
            Log.Warning("Cannot open serial port!");
            Log.Debug(exception.Message);
        }


    }

    private void _serialPort_ErrorReceived(object? sender, System.IO.Ports.SerialErrorReceivedEventArgs e)
    {
        Log.Error("serial port error " + e.EventType);
    }


    EventSimulator simulator = new EventSimulator();
    int numberOfSamples = 0;
    float[] samplesX = new float[1000], samplesY = new float[1000];
    decimal firstsum = 0, secondsum = 0;
    bool processing = false;
    private void SerialPort_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
    {
        //   var _serialPort = sender as SerialPort;


        if (!processing)
        {
            processing = true;




            //       if (_serialPort is null)
            //           return;
            //       if (!_serialPort.IsOpen)
            //           return;
            try
            {
                var pomosna = _serialPort.ReadLine();
                if (pomosna[0] == 'B')
                {
                    if (pomosna[4] == '1')
                    {
                        simulator.SimulateMousePress(SharpHook.Data.MouseButton.Button1);
                    }
                    else
                    {
                        simulator.SimulateMouseRelease(SharpHook.Data.MouseButton.Button1);
                    }
                    return;
                }

                float firstVariable = 0, secondVariable = 0;
                var text = pomosna.Split(',');
                if (text.Length >= 6)
                {
                    float.TryParse(text[4], out firstVariable);

                    float.TryParse(text[5], out secondVariable);
                }
                //za kalibracija na offset

                #region Calibration
                // avgX -0.64788  avgY -2.20256
                if (numberOfSamples < 500)
                {
                    samplesX[numberOfSamples] = firstVariable;
                    samplesY[numberOfSamples] = secondVariable;
                    numberOfSamples++;
                }
                else
                {
                    if (numberOfSamples == 500) Log.Information($"avgX {(samplesX.Sum() / 500f)}  avgY {(samplesY.Sum() / 500f)}");
                    numberOfSamples++;
                }

                firstVariable = firstVariable + 2.84788f;
                secondVariable = secondVariable + 0.60256f;
                #endregion

                #region Deadzone
                if (Math.Abs(firstVariable) < 2)
                    firstVariable = 0;
                if (Math.Abs(secondVariable) < 2)
                    secondVariable = 0;
                #endregion

                if (firstVariable == 0 && secondVariable == 0)
                    return;

                #region speed

                #endregion

                #region implementation

                if (imp == Implementation.Direktno)
                {
                    firstVariable *= 0.5f;
                    secondVariable *= 0.5f;
                    simulator.SimulateMouseMovementRelative((short)-firstVariable, (short)secondVariable);
                }
                if (imp == Implementation.SoIntegracija)
                {
                    firstVariable *= 1f;
                    secondVariable *= 1f;

                    firstsum += (decimal)-firstVariable;
                    secondsum += (decimal)secondVariable;
                    Debug.WriteLine($"firstsum: {firstsum} secondsum: {secondsum}");
                    simulator.SimulateMouseMovement((short)firstsum, (short)secondsum);
                }

                #endregion
                //    SerialOut.Text = $"{-(int)(y / 5 +0.4)}  {(int)(x/5 +1.15)}";

            }
            catch (Exception ex)
            {
                if (_serialPort.IsOpen)
                {
                    Log.Error("Failed to process serial recived event");
                    Log.Debug(ex.ToString());
                }
            }


            processing = false;
        }


    }

    enum Implementation
    {
        None = 0,
        /// <summary>
        /// vrednostite od giroskopo direktno sa staveni da mrda gluvceto relativno
        /// </summary>
        Direktno = 1,
        /// <summary>
        /// vrednostite od giroskopo se sumiraa 
        /// </summary>
        SoIntegracija = 2
    }
    Implementation imp = Implementation.Direktno;

    private void Discconect_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var prev = _serialPort.IsOpen;
        if (_serialPort is not null)
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        Log.Information($"Serial Port closing from {prev} to {_serialPort.IsOpen}");
    }


    public void AppendLog(string message)
    {
        if (LogTextBox != null)
        {
            // Make sure we are on the UI thread
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                LogTextBox.Text += message;
                LogTextBox.CaretIndex = LogTextBox.Text.Length; // scroll to end
            });
        }
    }


}

public class TextBoxSink : ILogEventSink
{
    private readonly Action<string> _logAction;
    private readonly IFormatProvider? _formatProvider;

    public TextBoxSink(Action<string> logAction, IFormatProvider? formatProvider = null)
    {
        _logAction = logAction;
        _formatProvider = formatProvider;
    }

    public void Emit(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage(_formatProvider);
        _logAction?.Invoke($"->{DateTime.Now} [{logEvent.Level}] {message}\n");
    }
}