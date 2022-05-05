#  Swell

[![GitHub release (latest by date)](https://img.shields.io/github/v/release/jothepro/doxygen-awesome-css)](https://github.com/TinyPHX/Swell/releases/latest)
[![GitHub](https://img.shields.io/github/license/jothepro/doxygen-awesome-css)](https://github.com/jothepro/doxygen-awesome-css/blob/main/LICENSE)
![GitHub Repo stars](https://img.shields.io/github/stars/jothepro/doxygen-awesome-css)

![](https://imgur.com/htcJB5A.png)

## Motivation

%Swell was started as a way to add water with float physics to a Unity project without attempting to control the look of the water. %Swell works great with most shaders but also looks just fine with the Unity standard shader. It's the perfect tool to get your water to feel and behave how you want before spending time getting the look correct. 

## Features

- 🌅 Create vast oceans or river systems
- 🧜 Easy to use right click context menu
- 🌊 Fully customizable and easy to use wave editor 
- 🌈 Highly optimized float physics
- 💧 Example scenes to demonstrate all functionality  
- 🧴 Works with any Unity rendering pipeline
- 🌐 Dynamic mesh controllable for your needs

## Quick Start Guide

To get started with %Swell all you need to do is, on the Hierarchy:
 - Right Click > %Swell > Water
 - Right Click > %Swell > Wave (Choose any)
 - Right Click > %Swell > Floater

![](https://imgur.com/yye0qXa.gif)

That should get you started and from there you can explore each Objects components 
to see how %Swell works.

## Components

There are 4 main components that hold %Swell together:

![](docs/images/swell_water_icon_22.png) **SwellWater** 

%Swell water is a dynamically sizeable water surface that pairs with a material  

![](https://imgur.com/cLlb6vx.png)

![](docs/images/swell_mesh_icon_22.png) **SwellMesh**

SwellMesh is completely managed by SwellWater but can exists on it's own as an independent 
component. It's main feature is that you can create a large water mesh with a more granular 
grid at the cent 

![](https://imgur.com/HecvMXT.png)

![](docs/images/swell_wave_icon_22.png) **SwellWave**

SwellWave lets you add any of several types of waves. They are all real time and fully customizable. 

![](https://imgur.com/7x7ehMg.png)

![](docs/images/swell_floater_icon_22.png) **SwellFloater**

Floaters can be attached to any regular Unity RigidBody3D to make it float!

![](https://imgur.com/kyqzE4U.png)

[//]: # (###Scripting Interface)

[//]: # ()
[//]: # (//Link to classes)

[//]: # (#### Scripts Examples)

[//]: # ()
[//]: # (TODO)

## Credits

Thanks to all the providers of these assets which were used in part in some of the demo scenes:

- [USSC Brig Sloop](https://assetstore.unity.com/packages/3d/vehicles/sea/brig-sloop-sailing-ship-77862)
- [Standard Assets](https://assetstore.unity.com/packages/essentials/asset-packs/standard-assets-for-unity-2018-4-32351)
- [SkySeries Freebie](https://assetstore.unity.com/packages/2d/textures-materials/sky/skybox-series-free-103633)
- [JMO Assets](https://assetstore.unity.com/packages/vfx/shaders/toony-colors-pro-2-8105#content)
- [Unity Standard Assets](https://docs.unity3d.com/530/Documentation/Manual/HOWTO-Water.html)

Special thanks to the creator and contributors of MyBox which was used to put the finishing touches on the UI, and saving me the  extensive headache of creating and maintaining custom unity inspector code. 

- [MyBox](https://github.com/Deadcows/MyBox)

Lastly thanks to the Doxigen team and creator of Doxygen Awesome which was a tremendous help in creating pretty docs!
- [Doxygen](https://www.doxygen.nl/index.html)
- [Doxygen Awesome](https://jothepro.github.io/doxygen-awesome-css/)

<span class="next_section_button">

Read Next: [Unity Demo Scenes](docs/demos.md)
</span>



