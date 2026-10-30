# A-Mirror-Apart-Source-Code
Original C# source code from A Mirror Apart, an isometric puzzle game built in Unity.


A Mirror Apart is an isometric puzzle game I have independently developed in Unity throughout high school.

This repository contains original C# source code I wrote for the game. It does not include the complete Unity project, artwork, models, audio, third-party assets, or commercial packages.

**Good Places to Start**

**Node.cs**
Builds and updates the navigation graph by detecting valid connections between nodes, allowing paths to change as level geometry moves.

**NodeWalker.cs**
Handles player movement and my A* pathfinding implementation, including stairs, ladders, moving geometry, redirection, animation states, and interaction with mirrored players.

**MirroredNodeWalker2.cs**
Transforms the original player’s movement across reflected coordinate systems and controls the corresponding mirrored players.

**BakedMeshInstanceSystem.cs**
Optimizes the game’s reflections by baking animated source meshes and rendering reflected copies with mesh instancing, including handling mirrored triangle winding.

**Tools and Third-Party Resources**
The game was built in Unity and coded in C# using Visual Studio. The full project also uses DOTween and several third-party visual assets. Third-party source code is not included in this repository.

**Demo**
A playable demo of A Mirror Apart is available on Steam.
https://store.steampowered.com/app/5274840/A_Mirror_Apart/

