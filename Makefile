all: build
BEPINEX_VERSION = 5

clean:
	@dotnet clean

restore:
	@dotnet restore

build: clean restore
	@dotnet build /p:BepInExVersion=$(BEPINEX_VERSION)

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