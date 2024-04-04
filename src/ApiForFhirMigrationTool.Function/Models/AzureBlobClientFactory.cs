using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ApiForFhirMigrationTool.Function.Models;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using EnsureThat;

namespace ApiForFhirMigrationTool.Function.Models
{ 
    public class AzureBlobClientFactory : IAzureBlobClientFactory
    {
        private readonly BlobServiceClient _blobServiceClient;
        public AzureBlobClientFactory(BlobServiceClient blobServiceClient)
        {
            _blobServiceClient = blobServiceClient;
        }
        public BlobContainerClient Create(string containerName)
        {
            EnsureArg.IsNotNullOrWhiteSpace(containerName, nameof(containerName));
            //BlobClient blobClient = _blobServiceClient.GetBlobContainerClient(containerName);
            BlobContainerClient blobContainerClient = _blobServiceClient.CreateBlobContainer(containerName);

            return blobContainerClient;
        }

        public BlobContainerClient GetBlobContainerClient(string containerName)
        {
            EnsureArg.IsNotNullOrWhiteSpace(containerName, nameof(containerName));
            //BlobClient blobClient = _blobServiceClient.GetBlobContainerClient(containerName);
            BlobContainerClient blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);

            return blobContainerClient;
        }
    }
}
