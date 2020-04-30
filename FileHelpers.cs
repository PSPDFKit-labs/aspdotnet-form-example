using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace PdfFormReadWrite
{
    public static class FileHelpers
    {
        private const long FileSizeLimit = 10 * 1024 * 1024;
        private static readonly string[] PermittedExtensions = {".pdf"};
        private static readonly byte[] PdfSignature = {0x25, 0x50, 0x44, 0x46};
        
        public static async Task<byte[]> ProcessFormFile(IFormFile formFile,
            ModelStateDictionary modelState)
        {
            if (formFile.Length == 0)
            {
                modelState.AddModelError(formFile.Name,
                    $"({formFile.FileName}) is empty.");

                return new byte[0];
            }

            if (formFile.Length > FileSizeLimit)
            {
                const long megabyteSizeLimit = FileSizeLimit / 1048576;
                modelState.AddModelError(formFile.Name,
                    $"({formFile.FileName}) exceeds " +
                    $"{megabyteSizeLimit:N1} MB.");

                return new byte[0];
            }

            try
            {
                await using var memoryStream = new MemoryStream();
                await formFile.CopyToAsync(memoryStream);

                // Check the content length in case the file's only
                // content was a BOM and the content is actually
                // empty after removing the BOM.
                if (memoryStream.Length == 0)
                {
                    modelState.AddModelError(formFile.Name,
                        $"({formFile.FileName}) is empty.");
                }

                if (!IsValidFileExtensionAndSignature(
                    formFile.FileName, memoryStream, PermittedExtensions))
                {
                    modelState.AddModelError(formFile.Name,
                        $"({formFile.FileName}) file " +
                        "type isn't permitted or the file's signature " +
                        "doesn't match the file's extension.");
                }
                else
                {
                    return memoryStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                modelState.AddModelError(formFile.Name,
                    $"({formFile.FileName}) upload failed. " +
                    $"Error: {ex.HResult}");
                // Log the exception
            }

            return new byte[0];
        }

        private static bool IsValidFileExtensionAndSignature(string fileName, Stream data, IEnumerable<string> permittedExtensions)
        {
            if (string.IsNullOrEmpty(fileName) || data == null || data.Length == 0)
            {
                return false;
            }

            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            if (string.IsNullOrEmpty(ext) || !permittedExtensions.Contains(ext))
            {
                return false;
            }

            data.Position = 0;

            // Check we have PDF data by checking the file signature.
            using var reader = new BinaryReader(data);
            var headerBytes = reader.ReadBytes(PdfSignature.Length);
            return headerBytes.Take(PdfSignature.Length).SequenceEqual(PdfSignature);
        }
    }
}
