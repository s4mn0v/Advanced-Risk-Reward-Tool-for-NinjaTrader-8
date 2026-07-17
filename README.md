# Advanced Risk/Reward Tool for NinjaTrader 8

advance-risk-reward is an enhanced Risk/Reward drawing tool that extends the native NinjaTrader utility. It is designed for traders who require precise position sizing and automation by integrating financial risk management directly into the chart interface.

### Overview

Unlike standard drawing tools, advance-risk-reward calculates the exact number of contracts or shares needed based on a user-defined dollar risk. It also synchronizes this calculation with the NinjaTrader ChartTrader for faster execution.

### Key Features

*   **Automated Position Sizing**: Calculates quantity based on a fixed "StopLoss (USD)" input and the distance between entry and stop levels.
*   **ChartTrader Integration**: Automatically fills the quantity selector in ChartTrader with the calculated position size.
*   **Partial Reward Levels**: Visualizes incremental target levels (1:1, 1:2, etc.) between the entry and the final target.
*   **Safety Limits**: Includes a "Max Contracts" setting to prevent the tool from setting quantities above a specific threshold.
*   **Multiple Display Units**: View risk and reward data in Currency, Ticks, Pips, or Percentage.
*   **Visual Customization**: Configurable background fills and strokes for stop and target zones.

### Installation

1. Close NinjaTrader 8.
2. Copy the **advance-risk-reward.cs** file into the following directory:
   `C:\Users\<YourUsername>\Documents\NinjaTrader 8\bin\Custom\DrawingTools`
3. Restart NinjaTrader 8.
4. The tool will appear in the Drawing Tools menu (default shortcut: F12) under the name **advance-risk-reward**.

### Usage and Settings

After drawing the tool on a chart by selecting the entry and stop points, you can modify its behavior in the Properties window:

*   **Ratio**: Sets the primary target distance relative to your risk.
*   **StopLoss (USD)**: Enter the maximum dollar amount you wish to risk per trade.
*   **Max Contracts**: Sets a ceiling for the quantity sent to ChartTrader.
*   **Modificar Cant Contratos**: Toggle this on to enable the automatic ChartTrader quantity update.
*   **Mostrar RRs**: Toggle this to show or hide intermediate 1:x reward levels.
*   **Display Unit**: Choose how price and risk information are formatted in the labels.

### Important Note

Any trades or investments made using this tool are at your own risk. Ensure you test the quantity auto-fill behavior on a simulated account before using it in a live environment.
