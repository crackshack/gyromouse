

#include <Arduino.h>
#include <BleMouse.h>
#include <MPU6050.h>

MPU6050 mpu;

int16_t ax, ay, az;
int16_t gx, gy, gz;

float Accx, Accy, Accz; // vrednosti u stilo na how to mechatronics
float Gyrox, Gyroy, Gyroz;
float elapsedTime, currentTime, previousTime;
float accAngleX, accAngleY, gyroAngleX, gyroAngleY, gyroAngleZ;
float pitch, roll, yaw;

float Prevpitch, Prevroll, Prevyaw; // kje zapishuvaa prethodnata vreddnost na pitch, roll i yaw
float Dpitch, Droll, Dyaw; // presmetanata razlika u pitch roll i yaw od prethodno dvizenje

float posX=0, posY=0;




BleMouse bleMouse;

void setup()
{
  // mpu6050
  Wire.begin();
  Serial.begin(115200);

  // Initialize device and check connection
  Serial.println("Initializing MPU...");
  mpu.initialize();
  Serial.println("Testing MPU6050 connection...");
  if (mpu.testConnection() == false)
  {
    Serial.println("MPU6050 connection failed");
    while (true)
      ;
  }
  else
  {
    Serial.println("MPU6050 connection successful");
  }

  // Use the code below to change accel/gyro offset values. Use MPU6050_Zero to obtain the recommended offsets
  Serial.println("Updating internal sensor offsets...\n");
  mpu.setXAccelOffset(0); // Set your accelerometer offset for axis X
  mpu.setYAccelOffset(0); // Set your accelerometer offset for axis Y
  mpu.setZAccelOffset(0); // Set your accelerometer offset for axis Z
  mpu.setXGyroOffset(0);  // Set your gyro offset for axis X
  mpu.setYGyroOffset(0);  // Set your gyro offset for axis Y
  mpu.setZGyroOffset(0);  // Set your gyro offset for axis Z
  // Print the defined offsets
  Serial.print("\t");
  Serial.print(mpu.getXAccelOffset());
  Serial.print("\t");
  Serial.print(mpu.getYAccelOffset());
  Serial.print("\t");
  Serial.print(mpu.getZAccelOffset());
  Serial.print("\t");
  Serial.print(mpu.getXGyroOffset());
  Serial.print("\t");
  Serial.print(mpu.getYGyroOffset());
  Serial.print("\t");
  Serial.print(mpu.getZGyroOffset());
  Serial.print("\n");

  // ble mouse

  bleMouse.begin();
}

void loop()
{

  Prevpitch = pitch;
  Prevroll = roll;
  Prevyaw = yaw;

  mpu.getMotion6(&ax, &ay, &az, &gx, &gy, &gz);

  Accx = ax / 16384;
  Accy = ay / 16384;
  Accz = az / 16384;
  accAngleX = (atan(Accy / sqrt(pow(Accx, 2) + pow(Accz, 2))) * 180 / PI) - 0.58;      // AccErrorX ~(0.58) See the calculate_IMU_error()custom function for more details
  accAngleY = (atan(-1 * Accx / sqrt(pow(Accy, 2) + pow(Accz, 2))) * 180 / PI) + 1.58; // AccErrorY ~(-1.58)

  // read gyro

  previousTime = currentTime;
  currentTime = millis();
  elapsedTime = (currentTime - previousTime) / 1000;

  Gyrox = gx / 131;
  Gyroy = gy / 131;
  Gyroz = gz / 131;

  // da dodam korekcija na vrednostite primer Gyrox = Gyrox + 0.58

  gyroAngleX = gyroAngleX + Gyrox * elapsedTime;
  gyroAngleY = gyroAngleY + Gyroy * elapsedTime;
  yaw = yaw + Gyroz * elapsedTime;

  roll = 0.96 * gyroAngleX + 0.04 * accAngleX;
  pitch = 0.96 * gyroAngleY + 0.04 * accAngleY;

  // Serial.print("a/g:\t");
  // Serial.print(ax); Serial.print("\t");
  // Serial.print(ay); Serial.print("\t");Serial.print(Accy);Serial.print("\t");
  // Serial.print(az); Serial.print("\t");Serial.print(Accz);Serial.print("\t");
  // Serial.print(gx); Serial.print("\t");Serial.print(ax);Serial.print("\t");
  // Serial.print(gy); Serial.print("\t");Serial.print(ax);Serial.print("\t");
  // Serial.println(gz);
  // Serial.print(Gyrox);
  // Serial.print("\t");
  // Serial.print(Gyroy);
  // Serial.print("\t");
  // Serial.print(Gyroz);

  // Print the values on the serial monitor

  Serial.print("roll>");
  Serial.print(roll);
  Serial.print("/");
  Serial.print("pitch>");
  Serial.print(pitch);
  Serial.print("/");
  Serial.print("yaw>");
  Serial.print(yaw);
  delay(10);
if(abs(pitch - Prevpitch) > 0.1){
  Dpitch = pitch - Prevpitch;
}
 
if(abs(pitch - Prevpitch) > 0.1){
  Droll = roll - Prevroll;
}
  
if(abs(yaw - Prevyaw) > 0.1){
  Dyaw = yaw - Prevyaw;
}
  

  Serial.print("Droll>");
  Serial.print(Droll);
  Serial.print("/");
  Serial.print("Dpitch>");
  Serial.print(Dpitch);
  Serial.print("/");
  Serial.print("Dyaw>");
  Serial.println(Dyaw);

  // mouse povrzuvanje

  // idea: da ima fiksen opseg na dvizenje preime od 45 do -45 za edna oska kade sho
  // sekoj stepen e pretstavuva lokacija na ekrano


  posX = posX + Dyaw;
  
  posY = posY + Dpitch;

  Serial.print("\t posX "); Serial.print(posX);Serial.print("\t posY "); Serial.print(posY);

  bleMouse.move(Dyaw * (-14), Dpitch * 12);

  Dyaw = 0;
  Dpitch = 0;
  Droll = 0;


}

/**
 * This example turns the ESP32 into a Bluetooth LE mouse that continuously moves the mouse.
 */
/*
#include <BleMouse.h>
#include <Arduino.h>

BleMouse bleMouse;

void setup() {
  Serial.begin(115200);
  Serial.println("Starting BLE work!");
  bleMouse.begin();
}

void loop() {
  if(bleMouse.isConnected()) {

    unsigned long startTime;

    Serial.println("Scroll up");
    startTime = millis();
    while(millis()<startTime+2000) {
      bleMouse.move(0,0,1);
      delay(100);
    }
    delay(500);

    Serial.println("Scroll down");
    startTime = millis();
    while(millis()<startTime+2000) {
      bleMouse.move(0,0,-1);
      delay(100);
    }
    delay(500);

    Serial.println("Scroll left");
    startTime = millis();
    while(millis()<startTime+2000) {
      bleMouse.move(0,0,0,-1);
      delay(100);
    }
    delay(500);

    Serial.println("Scroll right");
    startTime = millis();
    while(millis()<startTime+2000) {
      bleMouse.move(0,0,0,1);
      delay(100);
    }
    delay(500);

    Serial.println("Move mouse pointer up");
    startTime = millis();
    while(millis()<startTime+2000) {
      bleMouse.move(0,-1);
      delay(100);
    }
    delay(500);

    Serial.println("Move mouse pointer down");
    startTime = millis();
    while(millis()<startTime+2000) {
      bleMouse.move(0,1);
      delay(100);
    }
    delay(500);

    Serial.println("Move mouse pointer left");
    startTime = millis();
    while(millis()<startTime+2000) {
      bleMouse.move(-1,0);
      delay(100);
    }
    delay(500);

    Serial.println("Move mouse pointer right");
    startTime = millis();
    while(millis()<startTime+2000) {
      bleMouse.move(1,0);
      delay(100);
    }
    delay(500);

  }
}

*/