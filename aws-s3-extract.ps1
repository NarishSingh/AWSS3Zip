# Define the program path
$programPath = $PWD.Path + "\AWSS3Zip.exe"
 
# Define the argument to pass to the program
$argument = ""

foreach ($arg in $args) {
    if ($arg -like "*Server=*") {
        $argument += "`"" + $arg + "`"" + " "
        Write-Host "Arg contains server = $argument"
    }
    else {
        $argument += $arg + " "  # Append with a space between arguments
    }
}

# Combine the program path with the argument
$fullCommand = "$programPath $argument"

# Number of retries
$maxRetries = 100
$retryCount = 0

# Function to run the program
function Run-Program {
    try {
        Write-Host "Attempting to run the program with command: $fullCommand"
        
        # Run the program with the argument
        $process = Start-Process -FilePath $programPath -ArgumentList $argument -PassThru
        $process.WaitForExit()

        # Check if the process exited with a failure code (non-zero exit code)
        $tempPath = $PWD.Path + "\output"
        Write-Host "$tempPath"
        if (Get-ChildItem -Path "$tempPath") {
            throw "Did not process all files... re-running.."
        } 

        Write-Host "Program ran successfully!"
        return $true  # Success
    }
    catch {
        Write-Host "Error: $_"
        return $false  # Failure
    }
}

# Retry logic
while ($retryCount -lt $maxRetries) {
    $retryCount++
    $success = Run-Program

    if ($success) {
        break  # Exit the loop if the program ran successfully
    }
    else {
        Write-Host "Retrying... ($retryCount/$maxRetries)"
        Start-Sleep -Seconds 2  # Wait before retrying (optional)
    }
}

if (!$success) {
    Write-Host "Program failed after $maxRetries attempts."
}
