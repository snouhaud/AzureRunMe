// Courtesy of Steve Marx
// http://blog.smarx.com/posts/testing-existence-of-a-windows-azure-blob
using Microsoft.WindowsAzure.StorageClient;
public static class BlobExtensions
{
    public static bool Exists(this CloudBlob blob)
    {
        try
        {
            blob.FetchAttributes();
            return true;
        }
        catch (StorageClientException e)
        {
            if (e.ErrorCode == StorageErrorCode.ResourceNotFound)
            {
                return false;
            }
            else
            {
                throw;
            }
        }
    }
}