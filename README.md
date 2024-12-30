# **SmartStocker+**: Ultimate Auto-Stocking Plugin for Supermarket Simulator

## Overview

SmartStocker+ is a powerful BepInEx plugin designed to enhance your experience in **Supermarket Simulator** by automating stock management and providing advanced customization options. Say goodbye to manual stocking and optimize your supermarket operations with ease!

## Features

- **Auto-Stocking**: Automatically restock your shelves at the start of each day or on-demand.
- **Customizable Reserve Funds**: Protect a specified amount of money to avoid restocking when funds are low.
- **Dynamic Rack Management**: Adjust stock levels with a configurable multiplier.
- **Enhanced Label Display**: See detailed product and box information directly on racks and displays.
- **Keyboard Shortcuts**: Effortlessly control restocking and cart management.

### How It Works

The **SmartStocker+** plugin simplifies stock management by automating and optimizing product restocking. Here’s how it works:

1. **Automatic Calculation of Stock Needs**  
   The plugin evaluates the total number of products displayed in your store and calculates the number of additional products required to maintain full stock levels.

2. **Rack Stock Multiplier**  
   - This configurable multiplier determines the final volume of products to purchase.  
   - For example, if a display slot can hold 100 products and the Rack Stock Multiplier is set to `1.5`, the plugin will aim to maintain a stock level of 150 products.

3. **Dynamic Purchasing**  
   - The plugin subtracts the current inventory from the target stock level to calculate how many additional items need to be purchased.  
   - It prioritizes products with lower inventory to ensure critical items are always available.

4. **Box Optimization**  
   - The plugin considers the number of products per box when calculating purchases, ensuring efficient packing and minimizing unnecessary orders.

5. **Protected Funds**  
   - Purchases are made only if sufficient funds are available, taking into account a user-defined reserve (`ProtectedFunds`) to prevent overspending.

6. **Real-Time Adjustments**  
   - Auto-stocking runs at the start of each day or on-demand when triggered by a hotkey.  
   - Labels on racks and displays dynamically update to show stock levels and any deficits.

With these features, **SmartStocker+** ensures that your supermarket remains well-stocked, efficiently managed, and ready for customers at all times!

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
