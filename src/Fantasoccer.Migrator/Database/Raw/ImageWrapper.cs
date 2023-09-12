using System;
using System.IO;
using System.Threading.Tasks;
using Rystem.Business;

namespace FantacalcioV4.Database
{
    public class ImageWrapper : IData
    {
        public string Name { get; set; }
        public Task SaveAsync(Stream value, string contentType)
            => this.WriteBlobAsync(value, new Azure.Storage.Blobs.Models.BlobUploadOptions
            {
                HttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders() { ContentType = contentType }
            });
    }
}
