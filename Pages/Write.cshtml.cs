using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json.Linq;
using PdfFormReadWrite.Data;
using PSPDFKit;
using PSPDFKit.Providers;

namespace PdfFormReadWrite.Pages
{
    public class WriteModel : PageModel
    {
        private readonly IWebHostEnvironment _env;

        private readonly string _targetFilePath = Path.GetTempPath();
        [BindProperty] public BufferedSingleFileUploadPhysical FileUpload { get; set; }

        [BindProperty]
        public IList<FormFieldValue> FormFieldValues { get; set; } = new List<FormFieldValue>(new FormFieldValue[10]);

        public string Result { get; private set; }

        public WriteModel(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<IActionResult> OnPostUploadAsync()
        {
            if (!ModelState.IsValid)
            {
                Result = "Please correct the form.";

                return Page();
            }

            var formFileContent = await FileHelpers.ProcessFormFile(FileUpload.FormFile, ModelState);

            if (!ModelState.IsValid)
            {
                Result = "Please correct the form.";

                return Page();
            }

            // Save the PDF data received into a temporary file.
            var trustedFileNameForFileStorage = Path.GetRandomFileName();
            var filePath = Path.Combine(
                _env.WebRootPath, trustedFileNameForFileStorage);
            await using (var fileStream = System.IO.File.Create(filePath))
            {
                await fileStream.WriteAsync(formFileContent);
            }

            // Open the PDF and retrieve the form field values.
            var document = new Document(new FileDataProvider(filePath));
            var formFieldObject = new JObject();
            foreach (var formFieldValue in FormFieldValues)
            {
                if (string.IsNullOrEmpty(formFieldValue.Name)) continue;

                formFieldObject.Add(formFieldValue.Name, formFieldValue.Value);
            }

            document.GetFormProvider().SetFormFieldValuesJson(formFieldObject);
            document.Save(new DocumentSaveOptions());

            // Refresh the page with the form field data shown.
            return File(trustedFileNameForFileStorage, "application/octet-stream",
                FileUpload.FormFile.FileName);
        }
    }
}
