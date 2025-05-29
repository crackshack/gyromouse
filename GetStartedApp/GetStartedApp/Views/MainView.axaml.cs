using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
        Refresh_Click(null, new RoutedEventArgs());

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
        _serialPort.PortName = (DropDown.SelectedItem as PortDescription).Port;
        _serialPort.BaudRate = 115200;
        _serialPort.DataReceived += SerialPort_DataReceived;
        _serialPort.ErrorReceived += _serialPort_ErrorReceived;
        try
        {

            _serialPort.Open();
            Log.Information("Connected to serial port");
            _serialPort.DiscardOutBuffer();
            _serialPort.DiscardInBuffer();
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

    bool processing = false;
    EventSimulator simulator = new EventSimulator();
    int numberOfSamples = 0;
    float[] samplesX = new float[1000], samplesY = new float[1000];
    decimal firstsum = 0, secondsum = 0;
    private async void SerialPort_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
    {
        //   var _serialPort = sender as SerialPort;


        //  if (!processing)
        //   {
        //   processing = true;



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
                firstVariable /= 2f;
                secondVariable /= 2f;
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


        //  processing = false;
        //  }
        //flush old data
        //  if (_serialPort is null)
        //      return;
        //   if (!_serialPort.IsOpen)
        //       return;
        //  if (_serialPort.BytesToRead > 50)
        //      _serialPort.ReadLine();


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
    readonly Implementation imp = Implementation.Direktno;

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