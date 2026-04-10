# SelectPdf PDF Library for .NET with Html To Pdf Converter

[![SelectPdf](https://avatars.githubusercontent.com/u/11090785?v=4)](https://selectpdf.com)

SelectPdf for .NET is a professional **PDF library** that can be used for creating, writing, editing, handling and reading PDF files 
without any external dependencies within .NET applications. Using this .NET PDF library, you can implement rich capabilities to create PDF files from scratch 
or process existing PDF documents entirely through C#/VB.NET without installing Adobe Acrobat.

Many rich features can be supported by the .NET PDF API, such as security setting (including digital signatures), PDF merge/split, text, 
html and image drawing into pdf and many more. Besides, SelectPdf for .NET component can be used to easily convert **HTML to PDF** with C#/VB.NET in high quality. 
SelectPdf can also extract text from existing PDF documents or search for text in PDF documents and can convert PDF pages to raster images (PNG, BMP, JPEG, TIFF).

SelectPdf provides versions for .NET Framework and .NET Core 2.0 and above (through .NET Standard 2.0). 
This includes .NET 5 - .NET 8 and above. SelectPdf only works on Windows. SelectPdf works on Azure cloud, including Azure Web Apps (Basic plan or above) with some limitations.

## Main Features

### General Features

* Support for .NET Framework and .NET Core on Windows systems
* Generate PDF documents from scratch
* Load and modify existing PDF documents
* Set PDF document properties
* Set PDF document viewer preferences
* Set pdf page settings (size, orientation, margins)
* Set PDF document security settings (user password, permissions)
* Support for a large variety of pdf elements: text, image, html, shapes, links, bookmarks, etc
* Pdf templates to repeat elements in all pages of the generated PDF document
* Custom headers and footers for the generated pdf document
* Watermarks and stamps
* Support for page numbering
* Merge pdf documents
* Split pdf documents
* Extract pages from existing pdf documents
* Digital signatures
* Compressed pdf documents
* Support for pdf open actions (open to a specific page, execute javascript)
* Modify color space
* Support for PDF/A, PDF/X standards
* Advanced security settings (RC4 or AES encryption algorithms, up to 256 bits encryption keys)
* Form filling
* PDF portfolios management
* Resize/scale existing PDF content
 
### Html to Pdf Converter Features

* Convert any web page to pdf
* Convert any raw html string to pdf
* Set pdf page settings (page size, page orientation, page margins)
* Resize content during conversion to fit the pdf page
* Set pdf document properties
* Set pdf viewer preferences
* Set pdf security (passwords, permissions)
* Convert multiple web pages into the same pdf document
* Set conversion delay and web page navigation timeout
* Custom headers and footers
* Support for html in headers and footers
* Possibility to have different headers and footers for specific pages
* Automatic and manual page breaks
* Repeat html table headers on each page
* Support for @media types screen and print
* Support for internal and external links
* Generate bookmarks automatically based on html elements
* Support for HTTP headers
* Support for HTTP cookies
* Support for HTTP POST parameters
* Possibility to manually start the html to pdf conversion from Javascript
* Support for web pages that require authentication
* Support for proxy servers
* Possibility to convert only a section of a web page to pdf
* Possibility to exclude certain html elements from conversion
* Enable/disable javascript
* Modify color space
* Support for PDF/A, PDF/X standards
* Multithreading support
* HTML5/CSS3 support
* Web fonts support

### Html to Image Converter Features

* Convert any web page to image
* Convert any raw html string to image

### Pdf To Text Converter Features

* Extract Text from PDF
* Extract text at specific coordinates
* Search for Text in PDF

### Pdf To Image Converter Features

* Convert PDF Pages to Images

## Documentation and Samples

Online demo C#: [https://selectpdf.com/demo/](https://selectpdf.com/demo/)\
Online demo Vb.Net: [https://selectpdf.com/demo-vb/](https://selectpdf.com/demo-vb/)\
Online documentation: [https://selectpdf.com/pdf-library/](https://selectpdf.com/pdf-library/)


SelectPdf is very easy to use. For example, the following code will create a pdf document with a simple text in it:

```csharp
    SelectPdf.PdfDocument doc = new SelectPdf.PdfDocument();
    SelectPdf.PdfPage page = doc.AddPage();

    SelectPdf.PdfFont font = doc.AddFont(SelectPdf.PdfStandardFont.Helvetica);
    font.Size = 20;

    SelectPdf.PdfTextElement text = new SelectPdf.PdfTextElement(50, 50, "Hello world!", font);
    page.Add(text);

    doc.Save("test.pdf");
    doc.Close();
```


With SelectPdf is also very easy to convert any web page to a pdf document. The code is as simple as this:

```csharp
    SelectPdf.HtmlToPdf converter = new SelectPdf.HtmlToPdf();
    SelectPdf.PdfDocument doc = converter.ConvertUrl("https://selectpdf.com");
    doc.Save("test.pdf");
    doc.Close();
```

IMPORTANT: This package works for .NET Framework up to version 4.5. To use newer .NET Framework versions, .NET Core, .NET 5 - .NET 8 use the following package:
[Select.Pdf.NetCore](https://www.nuget.org/packages/Select.Pdf.NetCore/).

For complete product information, take a look at [SelectPdf](https://selectpdf.com) website.

