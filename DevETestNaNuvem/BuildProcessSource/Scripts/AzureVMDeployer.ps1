#
# Efetua operações para criação das VM's e configurações de ambientes
# Dependências http://azure.microsoft.com/pt-br/documentation/articles/install-configure-powershell/
#

#region [Parâmetros ]

	#
	# Parâmetros > origem build
	#

    param(
        [parameter(Mandatory=$true,
            Position=0,
            HelpMessage="Informe o nome da imagem para o ambiente!")]
            [alias("Img")]
            [string] $imageName,
        
        [parameter(Mandatory=$true,
            Position=1,
            HelpMessage="Informe o nome da VM!")]
            [alias("AzureEnviromentName")]
            [string] $vmName,

        [parameter(Mandatory=$true,
            Position=2,
            HelpMessage="Informe o nome da rede virtual do azure!")]
            [alias("AzureVnetName")]
            [string] $vnetName,

        [parameter(Mandatory=$true,
            Position=3,
            HelpMessage="Informe o nome da sub rede  virtual do azure!")]
            [alias("AzureSubNetName")]
            [string] $subNetName,

        [parameter(Mandatory=$true,
            Position=4,
            HelpMessage="Informe o ip para sua nova VM!")]
            [alias("EnviromentIp")]
            [string] $ip,

        [parameter(Mandatory=$true,
            Position=5,
            HelpMessage="Informe o usário administrador do dominio!")]
            [alias("EnviromentUser")]
            [string] $user,

        [parameter(Mandatory=$true,
            Position=6,
            HelpMessage="Informe a senha do administrador do dominio!")]
            [alias("EnviromentUserPwd")]
            [string] $pwd,

        [parameter(Mandatory=$true,
            Position=7,
            HelpMessage="Informe o dominio!")]
            [alias("EnviromentDomain")]
            [string] $domain,

        [parameter(Mandatory=$true,
            Position=8,
            HelpMessage="Informe o dominio!")]
            [alias("EnviromentDomainDnsSuffix")]
            [string] $domainDnsSuffix,
        
        [parameter(Mandatory=$true,
            Position=9,
            HelpMessage="Informe o diretorio do arquivo de publicação do azure!")]
            [alias("AzurePublishFilePath")]
            [string] $publishFilePath,

        [parameter(Mandatory=$true,
            Position=10,
            HelpMessage="Informe o nome do arquivo de publicação do azure!")]
            [alias("AzurePublishFileName")]
            [string] $publishFileName,

        [parameter(Mandatory=$true,
            Position=11,
            HelpMessage="Informe o azure subscription name!")]
            [alias("AzureSubsName")]
            [string] $azureSubscriptionName,

        [parameter(Mandatory=$true,
            Position=12,
            HelpMessage="Informe o azure storage account name!")]
            [alias("AzureStorageAccountName")]
            [string] $storageAccount
    )

    #
	# Parametros > locais
	#

    $scriptName = $MyInvocation.MyCommand.Name
    $publishFile =  [string]::Format("{0}{1}", $publishFilePath, $publishFileName)
    $deploymentSuccess = $false

#endregion

#region [Configuração]

	# Recupera o diretorio (corrente) de execução do script 
    function Get-ScriptDirectory
    {
        try {
           
            $Invocation = (Get-Variable MyInvocation -Scope 1).Value;
            if($Invocation.PSScriptRoot)
            {
                $Invocation.PSScriptRoot;
            }
            Elseif($Invocation.MyCommand.Path)
            {
                Split-Path $Invocation.MyCommand.Path
            }
            else
            {
                $Invocation.InvocationName.Substring(0,$Invocation.InvocationName.LastIndexOf("\"));
            }
        }
        catch {
        
            $deploymentSuccess = $false
            Write-Error "Ocorreu um erro  $_.Exception.ToString()"
        }
    }

    # Verifica e altera o contexto de execução para o modo "Run As Administrator"
	function Set-SecurityPrincipal
	{
		Write-Host  "Executando Set-SecurityPrincipal"
        
        $path = Get-ScriptDirectory
        $scriptPath = [string]::Format("{0}\{1}",$path, $scriptName)

		try {

            if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator"))
		    {   
                Write-Host "Alterando contexto de execução para administrador!"

                $relaunchArgs = '-ExecutionPolicy Unrestricted -file "' + $scriptPath + '" -IsRunAsAdmin'
                Start-Process "$PsHome\PowerShell.exe" -Verb RunAs -ArgumentList $relaunchArgs -PassThru

			    Break
		    }

        }
        catch {
        
            $deploymentSuccess = $false
            Write-Error "Ocorreu um erro  $_.Exception.ToString()"
           
        }
        
        Write-Host  "Fim Set-SecurityPrincipal"
    
	} 

    # Configura Assinatura do Azure e Storage Account

    function Set-AzureSubscriptionAndStorageAccount 
    {
         
        try {
             
            Import-AzurePublishSettingsFile $publishFile
            Set-AzureSubscription -SubscriptionName $azureSubscriptionName -CurrentStorageAccount $storageAccount
        }
        catch {
        
            $deploymentSuccess = $false
            Write-Error "Ocorreu um erro  $_.Exception.ToString()"
        }
    }

	# Cria maquina virtual do azure
	function Set-AzureEnvironment 
    {

        try 
        {
        
            # Obtêm localização do data center do Azure
		    $location = Get-AzureLocation -Verbose | 
			    Where { $_.DisplayName -eq "South Central US" } | Select-Object -First 1

		    # Obtêm imagem base para criação da VM 
		    $image = Get-AzureVMImage -Verbose | 
			    Where { $_.ImageName -eq $imageName } | 
			    sort PublishedDate -Descending | Select-Object -First 1

            # Configura e cria a VM
		    New-AzureVMConfig -Name $vmName -InstanceSize "Small" -ImageName $image.ImageName -Verbose |
			    Add-AzureProvisioningConfig -WindowsDomain -AdminUsername $user -Password $pwd -Domain $domain -DomainUserName $user -DomainPassword $pwd -JoinDomain $domainDnsSuffix -EnableWinRMHttp  |
			    Set-AzureSubnet -SubnetNames $subNetName |
			    Set-AzureStaticVNetIP -IPAddress $ip |
			    New-AzureVM -ServiceName $vmName -Location $location.Name -VNetName $vnetName -WaitForBoot -Verbose
        }
        catch {
        
            $deploymentSuccess = $false
            Write-Error "Ocorreu um erro  $_.Exception.ToString()"
        }
	}

	# Obtêm e instala certificado da VM recem criada
	function Get-AzureEnviromentCertificate 
    {

       try
       {
		 
		    $WinRMCertificateThumbprint = (Get-AzureVM -ServiceName $vmName -Name $vmName | Select-Object -ExpandProperty VM).DefaultWinRMCertificateThumbprint

            (Get-AzureCertificate -ServiceName $vmName -Thumbprint $WinRMCertificateThumbprint -ThumbprintAlgorithm SHA1).Data | Out-File "${env:TEMP}\AzureVMCloudService.tmp"
 
            $X509Object = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 "$env:TEMP\AzureVMCloudService.tmp"
            $X509Store = New-Object System.Security.Cryptography.X509Certificates.X509Store "Root", "LocalMachine"
            $X509Store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
            $X509Store.Add($X509Object)
            $X509Store.Close()
 
			Remove-Item "$env:TEMP\AzureVMCloudService.tmp"
        }
        catch {
        
            $deploymentSuccess = $false
            Write-Error "Ocorreu um erro  $_.Exception.ToString()"
        }
	}

	# Obtêm porta do endpoint para sessão remota do powershell
	function Get-AzurePSEndPoint 
    {

        try {

		 $vm = Get-AzureVM -ServiceName $vmName -Name $vmName 
		 $endpointPs1 = Get-AzureEndpoint -VM $vm -Name "PowerShell"
		 return $endpointPs1

        }
        catch {
        
            $deploymentSuccess = $false
            Write-Error "Ocorreu um erro  $_.Exception.ToString()"
        }

	}

    # Configura o firewall para instalação do agente de testes
	function Set-EnviromentFirewall($endpoint) 
    {
        
        try {

            Write-Host "Executando AlterarConfiguracoesFirewall" -ForegroundColor Magenta

		    $securePassword = ConvertTo-SecureString $pwd -AsPlainText -force
		    $credential = New-Object System.Management.Automation.PsCredential("$domain\$user", $securePassword)
		    $connectionUri =  [string]::Format("https://{0}.cloudapp.net:{1}",$vmName, $endpoint.Port)
    
		    Enter-PSSession -ConnectionUri $connectionUri -Credential $credential 
           
                Invoke-Command -ConnectionUri $connectionUri -Credential $credential -ScriptBlock { 
                        Set-ExecutionPolicy RemoteSigned -Scope LocalMachine -Force -ErrorAction SilentlyContinue
                        Update-Help -Force -ErrorAction SilentlyContinue
                        Set-NetFirewallProfile -Profile Domain,Private -Enabled False 
                }
            
		    Exit-PSSession

        }
        catch {
        
            $deploymentSuccess = $false
            Write-Error "Ocorreu um erro  $_.Exception.ToString()"
        }

        Write-Host "Finalizando AlterarConfiguracoesFirewall" -ForegroundColor Magenta
    }

	# Sequencia de execução das operações de criação de VM's
	function Set-AzureVMDeployer
	{
        try {           

            Set-ExecutionPolicy RemoteSigned -Scope LocalMachine -Force
            Set-AzureSubscriptionAndStorageAccount
		    Set-AzureEnvironment
		    Get-AzureEnviromentCertificate
		    $endpoint = Get-AzurePSEndPoint
		    Set-EnviromentFirewall($endpoint)
        
            $deploymentSuccess = $true

        }
        catch {
        
            $deploymentSuccess = $false
            Write-Error "Ocorreu um erro  $_.Exception.ToString()"
            Read-Host "Erro..."
        }


	}

#endregion

#region [Execução]

    try 
    { 
        Set-SecurityPrincipal
        Set-AzureVMDeployer
    }
    catch 
    {
        Write-Error "Ocorreu um erro  $_.Exception.ToString()"
    }

 exit $deploymentSuccess
    
#endregion