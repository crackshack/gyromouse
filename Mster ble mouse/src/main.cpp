/*
  Rui Santos & Sara Santos - Random Nerd Tutorials
  Complete project details at https://RandomNerdTutorials.com/esp-now-esp32-arduino-ide/
  Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files.
  The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
*/
#include <Arduino.h>

#include <esp_now.h>
#include <WiFi.h>

#if ARDUINO_USB_MODE
#warning This sketch should be used when USB is in OTG mode
void setup() {}
void loop() {}
#else
#include "USB.h"
#include "USBHIDVendor.h"
#include "USBHIDMouse.h"
#include "USBHIDKeyboard.h"
#endif
USBHIDVendor *Vendor = NULL;
USBHIDMouse *Mouse = NULL;
USBHIDAbsoluteMouse *AbsMouse = NULL;
USBHIDKeyboard *Keyboard = NULL;

enum MouseModes
{
  None = 0,
  Calibration = 1,
  Direct = 2,
  Integration = 3
};

class Configuration
{
public:
  MouseModes mouseMode = Integration;
  bool triggerCalibration = false;
  bool recenterTrigger = false;
  bool enableMouse = true;
};

Configuration config;

// Structure example to receive data
// Must match the sender structure
typedef struct struct_message
{
  float ax;
  float ay;
  float az;
  float gx;
  float gy;
  float gz;
} struct_message;

// Create a struct_message called myData
struct_message myData;

static void vendorEventCallback(void *arg, esp_event_base_t event_base, int32_t event_id, void *event_data)
{
  if (event_base == ARDUINO_USB_HID_VENDOR_EVENTS)
  {
    arduino_usb_hid_vendor_event_data_t *data = (arduino_usb_hid_vendor_event_data_t *)event_data;
    switch (event_id)
    {
    case ARDUINO_USB_HID_VENDOR_GET_FEATURE_EVENT:
      Serial.printf("HID VENDOR GET FEATURE: len:%u\n", data->len);
      break;
    case ARDUINO_USB_HID_VENDOR_SET_FEATURE_EVENT:
      Serial.printf("HID VENDOR SET FEATURE: len:%u\n", data->len);
      for (uint16_t i = 0; i < data->len; i++)
      {
        Serial.printf("0x%02X ", *(data->buffer));
      }
      Serial.println();
      break;
    case ARDUINO_USB_HID_VENDOR_OUTPUT_EVENT:
      Serial.printf("HID VENDOR OUTPUT: len:%u\n", data->len);
      for (uint16_t i = 0; i < data->len; i++)
      {
        Serial.write(Vendor->read());
      }
      break;

    default:
      break;
    }
  }
}

byte usbHIDVendorSize = 64;
float sumx = 16384 / 10;
float sumy = 16384 / 10;
// callback function that will be executed when data from esp_now is received
void OnDataRecv(const uint8_t *mac, const uint8_t *incomingData, int len)
{
  memcpy(&myData, incomingData, sizeof(myData));

  switch (config.mouseMode)
  {
  case Direct:
  {
    int8_t varx = -myData.gy - 3.11;
    int8_t vary = myData.gz + 0.5;

    if (abs(varx) < 2)
    {
      varx = 0;
    }
    if (abs(vary) < 2)
    {
      vary = 0;
    }

    // Mouse.move(varx, vary);
  }
  break;
  case Integration:
  {
    float varx = -myData.gy - 3.11 + 0.85 + 0.0599;
    float vary = myData.gz + 0.5 + 0.17 + 0.0252;

    sumx += varx;
    sumy += vary;

    Serial.print(">varx:");
    Serial.println(varx);
    Serial.print(">vary:");
    Serial.println(vary);
    Serial.print(">sumx:");
    Serial.println(sumx);
    Serial.print(">sumy:");
    Serial.println(sumy);

    if (abs(varx) <2 && abs(vary) < 2)
      AbsMouse->move(sumx * 10, sumy * 10);
  }
  break;
  }
}

void setup()
{
  /* ke se prfrli na komunikacija so usb hid
   * raboti so builtin USB bibliotekata za esp
   * samo treba usb mode da se podesi
   * vo USBDeview
   * posle so hidSharp se povrzi i bi trebalo da raboti
   * prvio bit na komunikacijata mora da bide repor id to
   * zemeno od https://nondebug.github.io/webhid-explorer/
   *
   *
   */

  // Initialize Serial Monitor
  Serial.begin(115200);

  Vendor = new USBHIDVendor();
  Vendor->onEvent(vendorEventCallback);
  Vendor->begin();
  Keyboard = new USBHIDKeyboard();
  Keyboard->begin();

  if (config.mouseMode == Direct)
  {
    Mouse = new USBHIDMouse();
    Mouse->begin();
  }
  if (config.mouseMode == Integration)
  {
    AbsMouse = new USBHIDAbsoluteMouse();
    AbsMouse->begin();
  }

  USB.begin();

  // Set device as a Wi-Fi Station
  WiFi.mode(WIFI_STA);

  // Init ESP-NOW
  if (esp_now_init() != ESP_OK)
  {
    Serial.println("Error initializing ESP-NOW");
    return;
  }

  // Once ESPNow is successfully Init, we will register for recv CB to
  // get recv packer info
  esp_now_register_recv_cb(esp_now_recv_cb_t(OnDataRecv));

  pinMode(0, INPUT_PULLUP);
}
bool B1_prevState = false;
void reportButtonState(int btn)
{
  if (digitalRead(btn) == B1_prevState)
  {
    B1_prevState = !B1_prevState;

    Serial.printf("B,%d,%d\r\n", btn, !digitalRead(btn));
  }
}
void loop()
{
  reportButtonState(0);
  //  delay(10);
}
