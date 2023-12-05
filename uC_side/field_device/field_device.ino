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
#define STEP_PIN3 3
#define STEPS_PER_REV 64
#define STEPS_PER_DEGREE 10  // the number of stepper motor steps per degree on the thermostat
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

//Function Prototypes

// event handler for when an mqtt message is recieved. Heavily modified version of the one found in 'mqtt_esp8266' example
void Callback(char* topic, byte* payload, unsigned int length);

// handles an mqtt client reconnect event: from 'mqtt_esp8266' example
void Reconnect();

// reads a temp from the dht sensor and sends it to the mqtt broker
void SendTemp();

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
  stepper.setSpeed(30);             // set stepper motor speed to 30 rpm
  lastReading = millis();           // get current time
  selectedTemp = (int)dht.readTemperature(); // get the current thermostat setting as the actual current temperature
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
  Serial.print("Message arrived [");
  Serial.print(topic);
  Serial.print("] ");
  for (int i = 0; i < length; i++) {
    Serial.print((char)payload[i]);
  }
  Serial.println();

  // Switch on the LED if an 1 was received as first character
  if ((char)payload[0] == '1') {
    digitalWrite(BUILTIN_LED, LOW);   // Turn the LED on (Note that LOW is the voltage level
    // but actually the LED is on; this is because
    // it is active low on the ESP-01)
  } else {
    digitalWrite(BUILTIN_LED, HIGH);  // Turn the LED off by making the voltage HIGH
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
  DynamicJsonDocument doc(200);

  doc["DataType"] = dataType;        // specify the temperature reading as air temp
  
  if (!dataType)        // if an air temperature is requested
  {
    doc["Temp"] = dht.readTemperature();
  }
  else 
  {
    doc["Temp"] = selectedTemp;
  }

  // serialize the payload
  char payload[200];
  serializeJson(doc, payload);

  client.publish(OUTTOPIC, payload);
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



