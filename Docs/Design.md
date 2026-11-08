# "Opera et Dies" and "Sub Corona"

"Opera et Dies" is the engine for a life simulator game. The engine is heavily data driven, and the data files for the current project can be found in "Sub Corona".

# Guiding Development Principles
The following principles should be kept in mind at all times when developing either "Opera et Dies" or "Sub Corona":

1. Data driven - As much as possible, the content and functionality of the game should be defined in JSON files that live in "Sub Corona". "Opera et Dies" should be limited to implementing the engine and display of the game. Specific mechanics could be represented in code if they can't be easily expressed in data file form.
2. Long term replayability - Since I'm probably the only one who will play the game, and since I'm interested in playing it more than once, it's important that the game be fun to replay. Randomness and branching events will be important to giving each play-through a unique feel.
3. Randomness needs a long tail - Whenever choosing random items from a list, items in the list should never have an equal chance of being selected. Some items should be more common than average and some should be less common than average. This helps preserve replayability, as it will take longer to have seen all the options in the list.
4. Customizability - It's important to allow the player to customize things. This can take multiple forms. The player should be able to customize the name, appearance, and personality traits of the villagers at the start of the game. The player should be able to provide extra JSON files that eliminate, edit, or add to the default data files provided by Sub Corona.

# "Opera et Dies" Overview

The engine will need to be able to do the following things:

1. Manage the map - The map is defined in data, but the engine will need to allow the player and NPCs to move about the map.
2. Manage the people - The NPCs in the game are fairly dynamic. They tend to have schedules to take care of work and needs, but they also have free time where their actions may be influenced by the level of influence the knight has over them.
3. Manage the passing of time - Time ostensibly follows a normal calender. However it is somewhat abbreviated in order to speed the passing of time. Seasons might affect some locations. Time of day passes as the player takes actions on the map.
4. Manage events - Certain events happen to NPCs as time passes (on a schedule or based on the NPC's location at specific times). The main character might encounter various random events. Events could be triggered by a number of different things (time, location, flags set by other events, sharing a location with an NPC)

# Technology

* The data in Sub Corona will be written as JSON files. The exact format of those files will be defined by what Opera et Dies supports. Since data files could be provided externally, reading those files will need to be fault tolerant and provide feedback to the user if something goes wrong.
* The code in Opera et Dies will be written in C# using WPF for display. Game logic should be located in view model classes and isolated from view concerns. This will help with testing.
* The display will be mostly text. There will be a few graphical elements, potentially with some simple animation.
* The GoldenAnvil.Utility package will be used for various common implementations that are used across projects outside the concerns of Opera et Dies. Changes to that package are allowed, but should not cause breakages for other potential projects. GoldenAnvil.Utility.Windows is for utility code with a dependence on System.Windows. If you are looking to implement something, first check to see if it is already implemented in GoldenAnvil.Utility or GoldenAnvil.Utility.Windows, or if there is already something similar in those packages.
* GoldenAnvil.Utility provides an async mechanism (TaskGroup) based on tasks that gives the ability to dynamically switch back and forth between sync and thread pool contexts. This is enabled by the Microsoft.VisualStudio.Threading package.
* GoldenAnvil.Utility provides a custom logging mechanism that can be written to the console and/or can be captured for display in the application.
* The current game state can be saved to a JSON file for later reloading. Since the data files could change before reloading the save, it's important that the loading functionality be resilient and clearly indicate the cause of any problems due to saved data that isn't supported by the currently loaded data.
* Use a Directory.Packages.props file to manage package versions centrally.
