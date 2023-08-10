# SCRATCH MAP TELEGRAM BOT
### Video Demo: https://vimeo.com/853356846
### Description:
This app is my Harvard's CS50 final project. It is a Telegram bot written in C# that generates an image of a map and highlights
the countries the user selected. It essentially creates a scratch map of the countries the user visited. The challenge
of the project comes from the fact that the app does not rely on any external APIs to style and retrieve map tiles, everything
is done using a local tile server.
### How It Works
1. The C# app containing the bot logic and an instance of [TileServer GL]("https://github.com/maptiler/tileserver-gl") are ran in Docker containers
2. TileServer GL serves tiles from a local **.mbtiles** file and styles them with a custom map style. TileServer GL dockerfile, configuration, map style and map tile files are located in **./tileserver** directory
3. The C# app uses [Telegram.Bot]("https://github.com/TelegramBots/Telegram.Bot") library to send requests to Telegram's Bot API. It uses long polling instead of webhooks as this approach requires less configuration and is enough for the scope of the project
4. The app is configured using **appsettings.json** for general configurations and **supportedCountries.json** for names, aliases, flags and map geometry of supported countries. I've decided to limit the scratch map scope to the European continent as the **.mbtiles** file size for that alone is almost 25 Gb. However, the bot can be easily reconfigured to generate images of any other region or even the whole planet, given that correct **.mbtiles** file is provided to TileServer GL and correct map boundaries are specified in the **appsettings.json**
5. When the user sends a message to the bot with a list of countries, they are looked up in supportedCountries.json, if at least one country is found, it's geometry is added as a layer to TileServer GL map style. Then the C# app sends a SIGHUP signal to TileServer GL Docker container using [Docker.NET]("https://github.com/dotnet/Docker.DotNet") library so that it would gracefully reload the map style with the added layer. Then the C# app requests the rendered raster tiles from the tile server, merges them into a bigger map image using [SkiaSharp]("https://github.com/mono/SkiaSharp") library and sends the image back to the user. The map style is then reverted to its original state and the generated image is deleted.
6. The tile coordinates are precalculated with respect to **.mbtiles** bounding box coordinates, which can be changed in **appsettings.json**
7. The approach I came up with has a performance bottleneck: only one image can be generated at a time as map style JSON file modification is involved for each request. The C# code uses a Semaphore to prevent race conditions. A possible workaround would be running multiple instances of TileServer GL and using some sort of load balancing, but this is out of the scope of this project
### Running the app
Running the app requires a few simple steps:
1. Download the **.mbtiles** file into **./tileserver** directory. I've tested the app with [MapTiler Europe](https://data.maptiler.com/downloads/europe/) **.mbtiles**. If you decide to use a different **.mbtiles** file, make sure you edit the **appsettings.json** and provide correct bounding box coordinates for your desired tile set.
2. Edit the **config.json** in the same directory. It should point to downloaded **.mbtiles** file in the **data** section and have correct bounding box coordinate values for your tile set in the **styles** section
3. Optional: edit the **custom-style.json** and **custom-style-backup.json** to improve on my pretty basic map style
4. Optional: if you decided to go with a different region, update the **supportedCountries.json** in **scr/ScratchMapApp.TelegramBot/Configuration** with the list of your desired region's countries following the existing schema. I've used [this](https://github.com/datasets/geo-countries) data for country geometry.
5. Create a Telegram bot by sending a message **@BotFather** and configure it. Obtain your bot token and insert it as the environment variable in **docker-compose.yml**
6. Run the app with **docker-compose up**
