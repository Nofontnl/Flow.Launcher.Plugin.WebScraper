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
The scrape configurations can be configured using JSON. An example is provided below.
```json
[
  {
    "Keyword": "example",
    "Tag": "Example template",
    "Url": "https://example.com",
    "ScrapeResults": [
      {
        "Title": "The header is: '${Header}'",
        "SubTitle": "The link reads: '${Link}'",
        "VariableBindings": {
          "Header": "body > div:nth-child(1) > h1:nth-child(1)",
          "Link": "body > div:nth-child(1) > p:nth-child(3) > a:nth-child(1)"
        }
      }
    ]
  }
]
```
where
* `Keyword`: keyword needed to be typed to scrape using a particular scrape configuration.
* `Tag`: tag to describe what this scrape configuration is for.
* `Url`: URL that leads to the page you want to scrape with a particular scrape configuration.
* `ScrapeResults`: list that contains result templates for a particular scrape configuration.
* `Title`: title template for this result template. Can contain variables using `${variable}` notation.
* `SubTitle`: subtitle template for this result template. Can contain variables using `${variable}` notation.
* `VariableBindings`: dictionary that maps variables to CSS selector strings. The CSS selectors strings will be matched when scraping and their matches will be inserted in the title and subtitle template.

> [!NOTE]
If a CSS selector matches multiple elements, multiple results will be generated when possible.

`scrape example` will scrape the header text, the link text, and the paragraph text using their CSS selectors. Two results will be displayed:
1. Title: `The header is: 'Example Domain'`  
Subtitle: `The link reads: 'Learn more'`
2. Title: `The paragraph says: 'This domain is for use in documentation examples without needing permission. Avoid use in operations.'`

CSS selectors can be copied using the element inspector in most web browsers, but whenever possible it is recommended to create your own in order for web scraping to work reliably.

