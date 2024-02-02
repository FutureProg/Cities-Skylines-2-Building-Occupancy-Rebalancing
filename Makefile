all: build
BEPINEX_VERSION = 5

clean:
	@dotnet clean BuildingOccupancyRebalancing.csproj

restore:
	@dotnet restore BuildingOccupancyRebalancing.csproj

build: clean restore
	@dotnet build BuildingOccupancyRebalancing.csproj /p:BepInExVersion=$(BEPINEX_VERSION)
	@cmd /c copy /y "bin\Debug\netstandard2.1\BuildingOccupancyRebalancing.dll" "E:\SteamLibrary\steamapps\common\Cities Skylines II\BepInEx\plugins\BuildingOccupancyRebalancing\BuildingOccupancyRebalancing.dll"
	@cmd /c copy /y "bin\Debug\netstandard2.1\0Harmony.dll" "E:\SteamLibrary\steamapps\common\Cities Skylines II\BepInEx\plugins\BuildingOccupancyRebalancing\0Harmony.dll"

run: 
	E:\SteamLibrary\steamapps\common\Cities Skylines II\Cities2.exe -developerMode

package-win:
	@-mkdir dist
	@cmd /c copy /y "bin\Debug\netstandard2.1\0Harmony.dll" "dist\"
	@cmd /c copy /y "bin\Debug\netstandard2.1\BuildingOccupancyRebalancing.dll" "dist\"
	@echo Packaged to dist/

package-unix: build
	@-mkdir dist
	@cp bin/Debug/netstandard2.1/0Harmony.dll dist
	@cp bin/Debug/netstandard2.1/BuildingOccupancyRebalancing.dll dist
	@echo Packaged to dist/