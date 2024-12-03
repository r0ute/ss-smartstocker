# **SmartStocker+**: Ultimate Auto-Stocking Plugin for Supermarket Simulator

## Overview

SmartStocker+ is a powerful BepInEx plugin designed to enhance your experience in **Supermarket Simulator** by automating stock management and providing advanced customization options. Say goodbye to manual stocking and optimize your supermarket operations with ease!

## Features

- **Auto-Stocking**: Automatically restock your shelves at the start of each day or on-demand.
- **Customizable Reserve Funds**: Protect a specified amount of money to avoid restocking when funds are low.
- **Dynamic Rack Management**: Adjust stock levels with a configurable multiplier.
- **Enhanced Label Display**: See detailed product and box information directly on racks and displays.
- **Keyboard Shortcuts**: Effortlessly control restocking and cart management:
  - Force auto-stock: `Ctrl + R`
  - Clean shopping cart: `Ctrl + C`
  - Toggle all restockers: `Ctrl + T`


## Configuration

The plugin provides an array of configurable settings to suit your preferences. Edit these in the generated `BepInEx/config` file or adjust them directly in the game's plugin settings menu.

| Setting                 | Description                                                               | Default Value       |
| ----------------------- | ------------------------------------------------------------------------- | ------------------- |
| **AutoStock**           | Enable or disable automated stocking.                                     | `true`              |
| **ProtectedFunds**      | Reserve a minimum amount of money that cannot be used for restocking.     | `$500`              |
| **RackStockMultiplier** | Multiplier to calculate the target stock level based on display capacity. | `1.0`               |
| **DisplayLabelInfo**    | Show additional details about products on display.                        | `true`              |
| **RackLabelInfo**       | Show detailed information on rack labels.                                 | `true`              |
| **Custom Colors**       | Customize label and box display colors (e.g., deficit or total count).    | Configurable via UI |

## Keyboard Shortcuts

| Action                | Default Shortcut |
| --------------------- | ---------------- |
| Force Auto-Stock      | `Ctrl + R`       |
| Clean Shopping Cart   | `Ctrl + C`       |
| Toggle All Restockers | `Ctrl + T`       |


Bring efficiency to your supermarket with **SmartStocker+**, your ultimate stock management solution!
