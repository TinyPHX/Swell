#  Swell

[![GitHub release (latest by date)](https://img.shields.io/github/v/release/jothepro/doxygen-awesome-css)](https://github.com/TinyPHX/Swell/releases/latest)
[![GitHub](https://img.shields.io/github/license/jothepro/doxygen-awesome-css)](https://github.com/jothepro/doxygen-awesome-css/blob/main/LICENSE)
![GitHub Repo stars](https://img.shields.io/github/stars/jothepro/doxygen-awesome-css)

![Gif of Float Algorythm](docs/images/float_algorythm_demo.gif)

## Motivation

Swell was started as a way to add water with float physics to a Unity project without attempting to control the look of the water. Swell works great with most shaders but also looks just fine with the Unity standard shader. It's the perfect tool to get your water to feel and behave how you want before spending time getting the look correct. 

## Features

- 🌅 Create massive oceans or river systems
- 🧜 Easy to use right click context menu
- 🌊 Fully customizable and easy to use wave editor 
- 🌈 Highly optimized float physics
- 💧 Example scenes to demonstrate all functionality  
- 🧴 Works with any Unity rendering pipeline
- 🌐 Dynamic mesh controllable for your needs

## Examples

- Sidebar-Only theme: [Documentation of this repository](https://jothepro.github.io/doxygen-awesome-css/)
- Base theme: [libsl3](https://a4z.github.io/libsl3/)

## Installation

Copy the file `doxygen-awesome.css` from this repository into your project or add this repository as submodule and check out the latest release:

```bash
git submodule add https://github.com/jothepro/doxygen-awesome-css.git
cd doxygen-awesome-css
git checkout v2.0.3
```

Choose one of the theme variants and configure Doxygen accordingly:

<span id="variants_image">

![Available theme variants](img/theme-variants.drawio.svg)

</span>

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

Further installation instructions:

- [How to install extensions](docs/extensions.md)
- [How to customize the theme (colors, spacing, border-radius, ...)](docs/customization.md)
- [Tips and Tricks for further configuration](docs/tricks.md)

## Browser support

Tested with

- Chrome 98, Chrome 98 for Android, Chrome 87 for iOS
- Safari 15, Safari for iOS 15
- Firefox 97, Firefox Daylight 97 for Android, Firefox Daylight 96 for iOS

## Credits

- This theme is inspired by the [vuepress](https://vuepress.vuejs.org/) static site generator default theme.
- Thank you for all the feedback on github!

<span class="next_section_button">

Read Next: [Extensions](docs/extensions.md)
</span>