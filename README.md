This program converts Bosch data acquisition files to the ASCII outing format used by Cosworth Pi Toolbox.

You will need to open your Bosch outing in WinDarab and then export the channels you want to a text file. 

Run Bosch2Pi to convert the text file to the Pi format, the output file has the same filename as the input file, but with the extension ".pi.txt".  
You will need a Pro level license for Pi Toolbox to open the generated ASCII file, but otherwise it will function the same as other outing file types.

Usage: Bosch2Pi <data_file> [-namemap <name_map_file>] [-outinginfo <outing_file>] [-constants <constants_file>] [-lapctr <lap_column_name>]

The namemap file allows you to change the name of telemetry channels during the conversion process.  A sample file is included and it is a simple name:value pair
to denote the old and new name of the channel.

The outinginfo file allows you to add outing properties to the new file such as car name, driver name, and track.  The common Pi Toolbox items are included in the 
sample file provided.  A '#' in front of a line comments it out so it is ignored during processing. Arbitrary name:value pairs can be added.

The constants file follows the same name:value format and allows you to add outing specific constants to the file such as vehicle specific data for math channels.
While the outing constant block format supports comments per constant, they aren't visibile in Pi Toolbox and aren't added to the resulting file.

Finally, this version uses a lap counter channel, if present, to add lap markers to the outing.  By default, the program looks for a channel named "lapctr" that
indicates the current lap number.  The -lapctr parameter allows you to specify a different channel name for the lap counter.
