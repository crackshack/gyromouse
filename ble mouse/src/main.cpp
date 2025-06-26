/*
https://lastminuteengineers.com/mpu6050-accel-gyro-arduino-tutorial/

bibliotekata za senzoro i nacino na komunikacija sa tolku mn testirani i probavani sho mi e omrznal zivoto
*/

/*
  Rui Santos & Sara Santos - Random Nerd Tutorials
  Complete project details at https://RandomNerdTutorials.com/esp-now-esp32-arduino-ide/
  Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files.
  The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
*/

#include <Wire.h>
#include "GY521.h"
#define DEBUG 1
#include <esp_now.h>
#include <WiFi.h>
GY521 sensor(0x68);

// 08:3a:f2:52:28:64 - za esp32
// 30:ed:a0:bb:21:04 - za esp32s3
//  REPLACE WITH YOUR RECEIVER MAC Address
//uint8_t broadcastAddress[] = {0x30, 0xed, 0xa0, 0xbb, 0x21, 0x04};
uint8_t broadcastAddress[] = {0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF};

// Structure example to send data
// Must match the receiver structure
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

esp_now_peer_info_t peerInfo;


// callback when data is sent
void OnDataSent(const uint8_t *mac_addr, esp_now_send_status_t status)
{
  if (!DEBUG)
  {
    Serial.print("\r\nLast Packet Send Status:\t");
    Serial.println(status == ESP_NOW_SEND_SUCCESS ? "Delivery Success" : "Delivery Fail");
  }
}

void setup()
{
  // Init Serial Monitor
  Serial.begin(115200);

  Wire.begin();

  while (sensor.wakeup() == false)
  {
    Serial.print(millis());
    Serial.println("\tCould not connect to GY521: please check the GY521 address (0x68/0x69)");
    delay(1000);
  }
  Serial.println("skiping connecting to gy521 oti testiram ble");
  sensor.setAccelSensitivity(3); //  2g
  sensor.setGyroSensitivity(3);  //  250 degrees/s

  sensor.setThrottle();
  Serial.println("start...");

  //  set calibration values from calibration sketch.
  sensor.axe = 0;
  sensor.aye = 0;
  sensor.aze = 0;
  sensor.gxe = 0;
  sensor.gye = 0;
  sensor.gze = 0;

  // Set device as a Wi-Fi Station
  WiFi.mode(WIFI_STA);

  // Init ESP-NOW
  if (esp_now_init() != ESP_OK)
  {
    Serial.println("Error initializing ESP-NOW");
    return;
  }

  // Once ESPNow is successfully Init, we will register for Send CB to
  // get the status of Trasnmitted packet
  esp_now_register_send_cb(OnDataSent);

  // Register peer
  memcpy(peerInfo.peer_addr, broadcastAddress, 6);
  peerInfo.channel = 0;
  peerInfo.encrypt = false;

  // Add peer
  if (esp_now_add_peer(&peerInfo) != ESP_OK)
  {
    Serial.println("Failed to add peer");
    return;
  }
}

void loop()
{
  
  sensor.read();
  float ax = sensor.getAccelX();
  float ay = sensor.getAccelY();
  float az = sensor.getAccelZ();
  float gx = sensor.getGyroX();
  float gy = sensor.getGyroY();
  float gz = sensor.getGyroZ();
  float t = sensor.getTemperature();

  myData.ax = ax;
  myData.ay = ay;
  myData.az = az;
  myData.gx = gx;
  myData.gy = gy;
  myData.gz = gz;

  if (DEBUG)
  {
    Serial.printf(">ax:%2f\r\n", ax);
    Serial.printf(">ay:%2f\r\n", ay);
    Serial.printf(">az:%2f\r\n", az);
    Serial.printf(">gx:%2f\r\n", gx);
    Serial.printf(">gy:%2f\r\n", gy);
    Serial.printf(">gz:%2f\r\n", gz);
  }

  // Send message via ESP-NOW
  esp_err_t result = esp_now_send(broadcastAddress, (uint8_t *)&myData, sizeof(myData));
  if (!DEBUG)
    if (result == ESP_OK)
    {
      Serial.println("Sent with success");
    }
    else
    {
      Serial.println("Error sending the data");
    }
 // delay(10);
}