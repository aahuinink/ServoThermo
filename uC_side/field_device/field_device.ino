/*
For use with the .NET MAUI dashboard app. Creates an MQTT client that reads ambient air temps every 30 seconds and publishes to the dashboard.
Also can set the thermostat temperature with a stepper motor remotely from the dashboard app.
ACKNOWLEDGEMENTS:
I used a fair bit of boilerplate code from other libraries in this project:
MQTT-related code has assistance from the "mqtt_esp8266" example from the PubSubClient library by Nick O'Leary.
DHT-related code had assistnace from the "DHTtester" example from Adafruit.
Json-related code had assistance from the "JsonParser" example from the ArduinoJson library v6.21.8 by Benoit Blanchon.
Stepper-related code had assistance from the "MotorKnob" example from the Stepper libray by Arduino.
*/
#include <Stepper.h>
#include <ArduinoJson.h>
#include <PubSubClient.h>
#include "DHT.h"
#include <WiFi.h>


// User defines
// stepper defines
#define STEP_PIN0 0
#define STEP_PIN1 1
#define STEP_PIN2 2
#define STEP_PIN3 1
#define STEPS_PER_REV 1024
#define STEPS_PER_DEGREE 100  // the number of stepper motor steps per degree on the thermostat
#define RPM 10                 // the motor speed in rpm (max motor speed is 14.6rpm)
//DHT defines
#define DHT_PIN 4
#define DHTTYPE DHT11
// WiFi defines
#define SSID "All My Homies Hate Rogers"
#define PASSWORD "fuckRogers!123"
// MQTT defines
#define BROKER "broker.emqx.io"
#define OUTTOPIC "ecet230/AaronH/MISO"
#define INTOPIC "ecet230/AaronH/MOSI"


// // Const Chars
// const char* ssid = "All My Homies Hate Rogers";
// const char* password = "fuckRogers!123";
// const char* mqtt_broker = "broker.emqx.io";


// Hardware Objects
DHT dht(DHT_PIN, DHTTYPE);
Stepper stepper(STEPS_PER_REV, STEP_PIN0, STEP_PIN1, STEP_PIN2, STEP_PIN3);
WiFiClient espClient;
PubSubClient client(espClient);   // for mqtt

// Global Variables
int selectedTemp;   // the currently selected thermostat temperature
long int lastReading;
double tempHistory[30];

//Function Prototypes

// event handler for when an mqtt message is recieved. Heavily modified version of the one found in 'mqtt_esp8266' example
void Callback(char* topic, byte* payload, unsigned int length);

// handles an mqtt client reconnect event: from 'mqtt_esp8266' example
void Reconnect();

// reads a temp from the dht sensor and sends it to the mqtt broker
void SendTemp(int dataType);

// set the thermostat to setTemp using the stepper motor
void SetThermostat(int setTemp);

// sets up a wifi connection: from 'mqtt_esp8266' example
void SetupWifi();








// -------- START OF MAIN PROGRAM BODY ----------- //

// Setup
void setup() {
  // put your setup code here, to run once:
  Serial.begin(115200);
  SetupWifi();
  client.setServer(BROKER, 1883);   // sets up mqtt client on the broker at port 1883
  client.setCallback(Callback);     // sets message recieved event handler
  client.subscribe(INTOPIC);       // subscribe to the mosi topic
  stepper.setSpeed(RPM);             // set stepper motor speed to 30 rpm
  lastReading = millis();           // get current time
  selectedTemp = (int)dht.readTemperature(); // get the current thermostat setting as the actual current temperature
  SendTemp(1);
}

// Main Loop
void loop() {
  // put your main code here, to run repeatedly:
  // boiler plate from mqtt example:
  if (!client.connected()) {
    Reconnect();
  }
  client.loop();
  // end of copied code

  if(millis() - lastReading > 30000)
  {
    SendTemp(0); // send the current air temp every 30s
    lastReading = millis();
  }

}

// ============== END OF MAIN PROGRAM BODY ================ //








// Function Definitions

void Callback(char* topic, byte* payload, unsigned int length) {
  // debugging boiler plate copied from mqtt example:
  Serial.print("Message arrived [");
  Serial.print(topic);
  Serial.print("] ");
  for (int i = 0; i < length; i++) {
    Serial.print((char)payload[i]);
  }
  Serial.println();

  // get json object
  DynamicJsonDocument doc(200); // added by me

  DeserializationError error = deserializeJson(doc, payload); // added by me

  // Test if parsing succeeds: copied from json example
  if (error) {
    Serial.print(F("deserializeJson() failed: "));
    Serial.println(error.f_str());
    return;
  }
  // end of copied code

  if(1 == doc["Query"])     // request for the selected temperature and temperature history
  {
    SendTemp(1);
  }
  else
  {
    SetThermostat(doc["SetTemp"]); // use the stepper motor to set the thermostat temperature
  }

}

void Reconnect() {
  // Loop until we're reconnected
  while (!client.connected()) { 
    Serial.print("Attempting MQTT connection...");
    // Create a random client ID
    String clientId = "ESP8266Client-";
    clientId += String(random(0xffff), HEX);
    // Attempt to connect
    if (client.connect(clientId.c_str())) {
      Serial.println("connected");
      // ... and resubscribe
      client.subscribe(INTOPIC);
    } else {
      Serial.print("failed, rc=");
      Serial.print(client.state());
      Serial.println(" try again in 5 seconds");
      // Wait 5 seconds before retrying
      delay(5000);
    }
  }
}

void SendTemp(int dataType)
{
  DynamicJsonDocument doc(1024);

  doc["DataType"] = dataType;        // specify the temperature reading as air temp
  for (int i = 1; i < 30; i++)
    {
      tempHistory[i-1] = tempHistory[i];    // shift the list down
    }
  // read temp and round it
  double temp = dht.readTemperature();
  temp *= 10;
  temp += 0.5;
  int itemp = (int)temp;
  temp = itemp / 10.0;
  tempHistory[29] = temp;

  if (!dataType)        // if an air temperature is requested
  {
    doc["CurrentTemp"] = temp;
  }
  else 
  {
    doc["CurrentTemp"] = (int)selectedTemp;
    for(int i = 0; i < 30; i++)
    {
      doc["TempHistory"][i] = tempHistory[i];
    }
  }

  // serialize the payload
  char payload[1024];
  serializeJson(doc, payload);

  client.publish(OUTTOPIC, payload);
  lastReading = millis();
  return;
}

void SetThermostat(int setTemp)
{
  stepper.step((setTemp - selectedTemp)*STEPS_PER_DEGREE);
  selectedTemp = setTemp;
  return;
}

void SetupWifi()
{
    delay(10);
  // We start by connecting to a WiFi network
  Serial.println();
  Serial.print("Connecting to ");
  Serial.println(SSID);

  WiFi.mode(WIFI_STA);
  WiFi.begin(SSID, PASSWORD);

  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }

  randomSeed(micros());

  Serial.println("");
  Serial.println("WiFi connected");
  Serial.println("IP address: ");
  Serial.println(WiFi.localIP());
}



