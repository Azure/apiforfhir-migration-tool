using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Azure.Storage.Blobs;

namespace ApiForFhirMigrationTool.Function.Models
{
    public interface IAzureBlobClientFactory
    {
        public BlobContainerClient Create(string containerName);
        public BlobContainerClient GetBlobContainerClient(string containerName);
    }
}
