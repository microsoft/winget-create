$expirationDate = { Get-Date }.Invoke().AddYears(5)
$pass = ConvertTo-SecureString -String "12345" -Force -AsPlainText
$thumbprint = (New-SelfSignedCertificate -Type Custom -NotAfter $expirationDate -Subject "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US" -KeyUsage DigitalSignature -FriendlyName "WingetCreate Test" -CertStoreLocation "Cert:\CurrentUser\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")).Thumbprint
Export-PfxCertificate -Cert cert:\CurrentUser\My\$thumbprint -FilePath WingetCreatePackage_TemporaryKey.pfx -Password $pass
Import-PfxCertificate -CertStoreLocation Cert:\LocalMachine\Root -FilePath WingetCreatePackage_TemporaryKey.pfx -Password $pass
