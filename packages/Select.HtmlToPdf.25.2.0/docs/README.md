# SelectPdf Html To Pdf Converter for .NET - Community Edition

[![SelectPdf](https://avatars.githubusercontent.com/u/11090785?v=4)](https://selectpdf.com)

SelectPdf offers a Community Edition (FREE) of the powerful **Html To Pdf Converter for .NET** that can be found in the full featured 
pdf library [SelectPdf for .NET](https://selectpdf.com/pdf-library-for-net/). The free html to pdf converter offers most of the features the professional 
sdk offers, the only notable limitation is that it can only generate pdf documents up to 5 pages long. 
SelectPdf Html To Pdf Converter provides versions for .NET Framework and .NET Core 2.0 and above (through .NET Standard 2.0). 
SelectPdf only works on Windows. SelectPdf works on Azure cloud, including Azure Web Apps (Basic plan or above) with some limitations.

## Main Features

* Generate pdf documents up to 5 pages
* Convert any web page to pdf
* Convert any raw html string to pdf
* Set pdf page settings (page size, page orientation, page margins)
* Resize content during conversion to fit the pdf page
* Set pdf document properties
* Set pdf viewer preferences
* Set pdf security (passwords, permissions)
* Set conversion delay and web page navigation timeout
* Custom headers and footers
* Support for html in headers and footers
* Automatic and manual page breaks
* Repeat html table headers on each page
* Support for @media types screen and print
* Support for internal and external links
* Generate bookmarks automatically based on html elements
* Support for HTTP headers
* Support for HTTP cookies
* Support for web pages that require authentication
* Support for proxy servers
* Enable/disable javascript
* Modify color space
* Multithreading support
* HTML5/CSS3 support
* Web fonts support

## Documentation and Samples

Online demo C#: [https://selectpdf.com/html-to-pdf/demo/](https://selectpdf.com/html-to-pdf/demo/)\
Online demo Vb.Net: [https://selectpdf.com/html-to-pdf/demo-vb/](https://selectpdf.com/html-to-pdf/demo-vb/)\
Online documentation: [https://selectpdf.com/html-to-pdf/docs/](https://selectpdf.com/html-to-pdf/docs/)


With SelectPdf is very easy to convert any web page to a pdf document. The code is as simple as this:

```csharp
    SelectPdf.HtmlToPdf converter = new SelectPdf.HtmlToPdf();
    SelectPdf.PdfDocument doc = converter.ConvertUrl("https://selectpdf.com");
    doc.Save("test.pdf");
    doc.Close();
```

IMPORTANT: Please note that THIS IS NOT A FREE TRIAL of our commercial library. This is a different, FREE product, that contains less features than the full library. 
If you want to test our full SelectPdf Library for .NET use one of the following urls:

https://selectpdf.com/pdf-library-for-net/ \
https://www.nuget.org/packages/Select.Pdf.NetCore/ \
https://www.nuget.org/packages/Select.Pdf/

IMPORTANT: This package works for .NET Framework up to version 4.5. To use newer .NET Framework versions, .NET Core, .NET 5 - .NET 8 use the following package:
https://www.nuget.org/packages/Select.HtmltoPdf.NetCore/

For complete product information, take a look at [SelectPdf Website](https://selectpdf.com).
