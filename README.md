#  Swell

[![GitHub release (latest by date)](https://img.shields.io/github/v/release/jothepro/doxygen-awesome-css)](https://github.com/TinyPHX/Swell/releases/latest)
[![GitHub](https://img.shields.io/github/license/jothepro/doxygen-awesome-css)](https://github.com/jothepro/doxygen-awesome-css/blob/main/LICENSE)
![GitHub Repo stars](https://img.shields.io/github/stars/jothepro/doxygen-awesome-css)

![](docs/images/banner.png)

## Motivation

Swell was started as a way to add water with float physics to a Unity project without attempting to control the look of the water. Swell works great with most shaders but also looks just fine with the Unity standard shader. It's the perfect tool to get your water to feel and behave how you want before spending time getting the look correct. 

## Features

- 🌅 Create vast oceans or river systems
- 🧜 Easy to use right click context menu
- 🌊 Fully customizable and easy to use wave editor 
- 🌈 Highly optimized float physics
- 💧 Example scenes to demonstrate all functionality  
- 🧴 Works with any Unity rendering pipeline
- 🌐 Dynamic mesh controllable for your needs

## Quick Start Guide

### Overview


### Step by step

To get started with Swell all you need to do is open your new scene


## Components

There are 4 main components that hold Swell together:

![](docs/images/swell_water_icon_22.png) **SwellWater** 

Swell water is a dynamically sizeable water surface that pairs with a material  

![](docs/images/component_swell_water.png)

![](docs/images/swell_mesh_icon_22.png) **SwellMesh**

SwellMesh is completely managed by SwellWater but can exists on it's own as an independent 
component. It's main feature is that you can create a large water mesh with a more granular 
grid at the cent 

![](docs/images/component_swell_mesh.png)

![](docs/images/swell_wave_icon_22.png) **SwellWave**

![](docs/images/component_swell_wave.png)

![](docs/images/swell_floater_icon_22.png) **SwellFloater**

![](docs/images/component_swell_floater.png)


- ![](docs/images/swell_water_icon_22.png) **SwellWater** - 
- ![](docs/images/swell_mesh_icon_22.png) **SwellMesh** -
- ![](docs/images/swell_wave_icon_22.png) **SwellWave** -
- ![](docs/images/swell_floater_icon_22.png) **SwellFloater** -

|  SwellWater |
|-----------------------------------------------------|
| ![](docs/images/component_swell_water.png)          |

| ![](docs/images/swell_wave_icon_22.png) SwellWave |
|---------------------------------------------------|
| [](docs/images/component_swell_wave.png)          |

|SwellMesh                                 | SwellFloater                                 |
|------------------------------------------|----------------------------------------------|
|![](docs/images/component_swell_mesh.png) | ![](docs/images/component_swell_floater.png) |

###Scripting Interface

//Link to classes

#### Scripts Examples


## Installation

Copy the file `doxygen-awesome.css` from this repository into your project or add this repository as submodule and check out the latest release:

```bash
git submodule add https://github.com/jothepro/doxygen-awesome-css.git
cd doxygen-awesome-css
git checkout v2.0.3
```

1. **Base theme**:
```
# Doxyfile
GENERATE_TREEVIEW      = YES # optional. Also works without treeview
HTML_EXTRA_STYLESHEET  = doxygen-awesome-css/doxygen-awesome.css
```

2. **Sidebar-only theme**:
```
# Doxyfile
GENERATE_TREEVIEW      = YES # required!
HTML_EXTRA_STYLESHEET  = doxygen-awesome-css/doxygen-awesome.css \
                         doxygen-awesome-css/doxygen-awesome-sidebar-only.css
```

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



