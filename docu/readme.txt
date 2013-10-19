Store media files in Azure Storage

You have to configure the file system provider for your Azure Storage Account in umbraco /config in file FileSystemProviders.config.
Here is an example config:

<?xml version="1.0"?>
<FileSystemProviders>

	<!-- Media -->
	<!-- parameters order is important ! -->
	<!-- place this in your umbraco instanze in the /config folder -->
	
	<Provider alias="media"
			  type="idseefeld.de.UmbracoAzure.AzureBlobFileSystem, idseefeld.de.UmbracoAzure">
		<Parameters>
			<add key="containerName" value="media" />
			<add key="rootUrl" value="http://[myAccountName].blob.core.windows.net/" />
			<add key="connectionString" value="DefaultEndpointsProtocol=https;AccountName=[myAccountName];AccountKey=[myAccountKey]"/>
		</Parameters>
	</Provider>

</FileSystemProviders>
