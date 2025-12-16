# Project Usage

This guide explains how to run and configure the PowerPosition application.

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (version 8.0 or compatible)

## How to Run

1.  **Open your terminal** (Command Prompt, PowerShell, or Terminal).
2.  **Navigate to the project directory**:
    Replace `path\to\project` with the actual path where you cloned or extracted the project.
    ```powershell
    cd c:\Users\UserName\Documents\tests\axso_etrm_coding_challenge
    ```
3.  **Run the application**:
    ```powershell
    dotnet run
    ```

## Configuration

The application determines the output folder for the CSV files and the interval (in minutes) for data extraction in the following order of precedence:

### 1. Command Line Arguments
You can pass the **Output Folder** and **Interval (minutes)** directly when running the application.

**Format:** `dotnet run <OutputFolder> <IntervalMinutes>`

**Example:**
To save files to `C:\temp` every `1` minute:
```powershell
dotnet run "C:\temp" 1
```

### 2. Configuration File (`appsettings.json`)
If no command-line arguments are provided, the application looks for an `appsettings.json` file in the execution directory.

**Example `appsettings.json`:**
```json
{
  "OutputFolder": "C:\\Users\\UserName\\Documents",
  "IntervalMinutes": 5
}
```
*Note: Ensure the `OutputFolder` exists.*

### 3. Default Settings
If neither command-line arguments nor a valid configuration file are found, the application uses default values:

- **Output Folder**: The current working directory (where the app is running).
- **Interval**: 15 minutes.

## Logs
Logs are generated in the `logs` folder (and console output) and provide details about the execution, including:
- Service start/stop.
- Execution times.
- CSV file creation.
- Errors and retries.
