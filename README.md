# ServoThermo

ServoThermo is a prototype home temperature monitoring and control software for old mechanical dial-type thermostats.

A microcontroller monitors the temperature and in a room and controls a servo connected to the thermostat dial. The servo sets the thermostat dial to a temperature set by the user.
The microcontroller sends the temperature to an MQTT broker where the associated web interface written in .NET MAUI displays the temperature history and allows the user to set the temperature via MQTT.
