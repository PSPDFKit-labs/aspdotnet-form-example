using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json.Linq;
using PdfFormReadWrite.Data;
using PSPDFKit.Providers;

namespace PdfFormReadWrite.Pages
{
    public class ReadModel : PageModel
    {
        private readonly string _targetFilePath = Path.GetTempPath();

        [BindProperty] public BufferedSingleFileUploadPhysical FileUpload { get; set; }

        [BindProperty(SupportsGet = true)]
        public IList<FormFieldValue> FormFieldValues { get; } = new List<FormFieldValue>();

        [BindProperty(SupportsGet = true)] public string FormFieldsJson { get; set; } = null;

        public string Result { get; private set; }

        public void OnGet()
        {
            if (FormFieldsJson == null) return;

            var formFieldsJson = JObject.Parse(FormFieldsJson);
            foreach (var (key, value) in formFieldsJson)
            {
                FormFieldValues.Add(new FormFieldValue {Name = key, Value = value.ToString()});
            }
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
                _targetFilePath, trustedFileNameForFileStorage);
            await using (var fileStream = System.IO.File.Create(filePath))
            {
                await fileStream.WriteAsync(formFileContent);
            }

            // Open the PDF and retrieve the form field values.
            var document = new PSPDFKit.Document(new FileDataProvider(filePath));
            var fieldValuesJson = document.GetFormProvider().GetFormFieldValuesJson();

            // Refresh the page with the form field data shown.
            return RedirectToPage(new {FormFieldsJson = fieldValuesJson.ToString()});
        }
    }
}
