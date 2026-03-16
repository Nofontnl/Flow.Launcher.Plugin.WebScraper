# Flow.Launcher.Plugin.WebScraper


A plugin that lets you scrape information from webpages and display them in the [Flow launcher](https://github.com/Flow-Launcher/Flow.Launcher).

## Installation
```
pm install Web Scraper
```

## Usage

    scrape <config_keyword>

where `config_keyword` is the keyword set in one of the scrape configurations.

### Scrape configurations
The plugin can be configured using JSON. An example is provided below.
```json
{
  "ScrapeConfigs": [
    {
      "Keyword": "example",
      "Tag": "Example template",
      "Url": "https://example.com",
      "ScrapeResults": [
        {
          "Title": "The header is: '${Header}'",
          "SubTitle": "The link reads: '${Link}'",
          "VariableBindings": {
            "Header": "/html/body/div/h1",
            "Link": "/html/body/div/p[2]/a"
          }
        },
        {
          "Title": "The paragraph says: '${Paragraph}'",
          "SubTitle": "",
          "VariableBindings": {
            "Paragraph": "/html/body/div/p[1]"
          }
        }
      ]
    }
  ]
}
```
where
* `ScrapeConfigs`: list that contains all scrape configurations.
* `Keyword`: keyword needed to be typed to scrape using a particular scrape configuration.
* `Tag`: tag to describe what this template is for.
* `Url`: URL that leads to the page you want to scrape with a particular scrape configuration.
* `ScrapeResults`: list that contains result templates for a particular scrape configuration.
* `Title`: title template for one (or more) scrape results.* Can contain variables using `${variable}` notation.
* `SubTitle`: subtitle template for one (or more) scrape results.* Can contain variables using `${variable}` notation.
* `VariableBindings`: dictionary that maps variables to XPaths. The XPaths will be evaluated when scraping and inserted in the title and subtitle template.

> [!NOTE]
\* If an XPath matches multiple elements, multiple results will be generated when possible.

`scrape example` will scrape the header text, the link text, and the paragraph text using their XPaths. Two results will be displayed:
1. Title: `The header is: 'Example Domain'`  
Subtitle: `The link reads: 'Learn more'`
2. Title: `The paragraph says: 'This domain is for use in documentation examples without needing permission. Avoid use in operations.'`

XPaths can be copied using the element inspector in most web browsers.

