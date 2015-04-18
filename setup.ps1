$instanceName = "Demo"

$serverConfigPath = "C:\Octopus\Server\OctopusServer.config"
$serverPath = "C:\Octopus\Server"

$tentacleHome = "C:\Octopus\Tentacle"
$tentacleConfigPath = "C:\Octopus\Tentacle\Tentacle.config"
$appsPath = "C:\Octopus\Applications"

&"C:\Program Files\Octopus Deploy\Octopus\Octopus.Server.exe" delete-instance --instance $instanceName
&"C:\Program Files\Octopus Deploy\Tentacle\Tentacle.exe" delete-instance --instance $instanceName

&"C:\Program Files\Octopus Deploy\Octopus\Octopus.Server.exe" create-instance --instance $instanceName --config $serverConfigPath
&"C:\Program Files\Octopus Deploy\Octopus\Octopus.Server.exe" configure --instance $instanceName --home $serverPath --storageMode "Embedded" --upgradeCheck "True" --upgradeCheckWithStatistics "True" --webAuthenticationMode "UsernamePassword" --webForceSSL "False" --webListenPrefixes "http://localhost:80/" --storageListenPort "10931" --commsListenPort "10943"
&"C:\Program Files\Octopus Deploy\Octopus\Octopus.Server.exe" admin --instance $instanceName --username "Redgate" --password "Redg@te1" --wait "5000"
&"C:\Program Files\Octopus Deploy\Octopus\Octopus.Server.exe" license --instance $instanceName --free
&"C:\Program Files\Octopus Deploy\Octopus\Octopus.Server.exe" service --instance $instanceName --install --reconfigure --start

$thumbOut = &"C:\Program Files\Octopus Deploy\Octopus\Octopus.Server.exe" show-thumbprint --instance $instanceName
$thumbPrint = $thumbOut[$thumbOut.Length - 2]
"Octopus Thumbprint : $thumbPrint"

&"C:\Program Files\Octopus Deploy\Tentacle\Tentacle.exe" create-instance --instance $instanceName --config $tentacleConfigPath --console
&"C:\Program Files\Octopus Deploy\Tentacle\Tentacle.exe" new-certificate --instance $instanceName --console
&"C:\Program Files\Octopus Deploy\Tentacle\Tentacle.exe" configure --instance $instanceName --home $tentacleHome --console
&"C:\Program Files\Octopus Deploy\Tentacle\Tentacle.exe" configure --instance $instanceName --app $appsPath --console
&"C:\Program Files\Octopus Deploy\Tentacle\Tentacle.exe" configure --instance $instanceName --port "10933" --console
&"C:\Program Files\Octopus Deploy\Tentacle\Tentacle.exe" configure --instance $instanceName --trust $thumbPrint --console
&"C:\Program Files\Octopus Deploy\Tentacle\Tentacle.exe" service --instance instanceName --install --start --console