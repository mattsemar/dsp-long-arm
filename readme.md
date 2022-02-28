Long Arm

Provides tools to make planet-wide building a little easier. Open UI with Control+L (configurable in Settings). On first load, inspect and bot build range will be extended, 
but no Build Helper Modes will be enabled enabled. 

Does not affect saved games, build range changes are reverted prior to the game saving its data

### Build Helper Modes

In addition to extending build range, additional modes are provided to make building planet wide blueprints a little less painful. These are optional, but may
be useful in certain situations. All 3 of the active ones will work even when the build range is not overridden.

* FlyToBuild - queues up commands to send the mecha to nearby build previews (until all are built)
* FastBuild - slightly cheaty option where builds are done as quickly as the game allows but inventory items _are_ consumed, however bot travel time is ignored
* FreeBuild - very cheaty option where inventory items are not consumed, builds are done as quickly as the game allows. This mode can disable achievements for your save. This mode is generally
limited by the number of updates per second your computer can do while adding factory machines at a very high speed.
* None - default setting

Note that the helper mode used is saved in config now so be cautious when switching between different save games to make sure the mode you want is the one currently selected. 

### Factory Spray
Use spray from inventory to spray factory items on belts, in assembler input slots, in generators (even ray receivers). 
Enable `SprayStationContents` and `SprayInventoryContents` in config to also spray inventory or station contents

This feature was built for cases where adding a spray coater to an existing production line
takes a long time to go into effect (lenses in ray receivers). This helps by jumpstarting the process a bit.

Note that when FreeBuild mode is enabled then no spray from inventory will be used.

### Factory Tour Mode

(Default keybind to open window is Control + W)

Designed to help locate parts of the current planet/factory by building a list of locations and then flying mecha to each location in list.

![Tour](https://github.com/mattsemar/dsp-long-arm/blob/master/Examples/Tour.png?raw=true)

Supports finding: veins, stations, generators, storage and assemblers. Use item filter to narrow results down.

Once list is built Next/Previous buttons will issue fly command in mecha to go to next location.

Note: locations for veins and assemblers are only considered distinct if they are more than 10 meters apart

### KeyBinds 

Open game settings to rebind or unbind the keys under the Control tab
![Config](https://github.com/mattsemar/dsp-long-arm/blob/master/Examples/keybinds.png?raw=true)

### Installation

This mod requires BepInEx to function, download and install it
first: [link](https://bepinex.github.io/bepinex_docs/master/articles/user_guide/installation/index.html?tabs=tabid-win)

#### Manually

First install the [CommonAPI](https://dsp.thunderstore.io/package/CommonAPI/CommonAPI/) mod, this mod depends on it.
Next, download the archive file, extract contents and drag `LongArm.dll` into the `BepInEx/plugins` directory. 

#### Mod manager

Click the `Install with Mod Manager` link above. Make sure dependencies are installed, if prompted. This mod will do basically nothing if the keybinds don't get registered, keybinds are 
now supplied from CommonAPI

## Changelog

#### v1.4.1
Update: Added additional assembler types for spray targeting (labs, silos & ejectors)  

#### v1.4.0
Update: Added support for spraying factory items from inventory  

#### v1.3.6
Update: Add ability to override drag build range. Thanks Raptor for suggestion  

#### v1.3.5
Update: Add fuel should now preserve proliferator points on added fuel items  

#### v1.3.4
Bugfix: reverted change to FastBuild/FreeBuild mode (thanks Speedy for report)  

#### v1.3.3
Bugfix: fixed issue where artificial stars would not be refilled (thanks for report Valoneu) 

#### v1.3.2
Fixed issue where username shows up as altuser

#### v1.3.1
Update: Update to work with game version released 20-Jan-2022 (0.9.24.11187), make sure to update to CommonAPI 1.3+

#### v1.3.0
Removed window showing prebuild status summary since this functionality is now part of the game.
Build helper mode now persisted between restarts.
Removed "Auto" option from tour mode 

#### v1.2.2
Updated build

#### v1.2.1
Resolved FastBuild/FreeBuild mode performance issue on planets with lots of assembling machines. Should be much snappier now.
Adjusted height of tour window

#### v1.2.0
Added support for adding fuel to all generators across all factories (hold CTRL while hitting button)
Switched to CommonAPI for keybind registration

#### v1.1.3
Bugfix - fixed issue with FastBuild mode when returning to planet with unrealized prebuilds by delaying action while player is in "sail" mode.   

#### v1.1.1/2
Rebuild against latest game version

#### v1.1.0
Fixed exception thrown when preview window layout changes
Added factory tour mode to help locate things on planet factory faster

#### v1.0.1
Rebuild to sync with latest version of game

#### v1.0.0
Added Build Status window to show blueprinted items vs. inventory counts
Added "Add Bots" button for filling stations all stations on planet with drones/vessels (configurable)
Tweaked Free & Fast Build modes to run much more quickly 

#### v0.2.0
Added "Add Fuel" action for jumpstarting power network with missing fuel from player inventory
Fixed issue with count of remaining prebuilds in FastBuild mode

#### v0.1.0
Made build helper modes independent from settings to extend build/inspect range

#### v0.0.1
First version

## Contact
Bugs? Contact me on discord: mattersnot#1983 or create an issue in the github repository.
