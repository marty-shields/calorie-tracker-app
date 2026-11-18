Feature: Food titles
  A food reads the same wherever it is listed: the title Core works out (the brand and name, see FoodDisplay) heads the search row, the food's values, the Serving screen and the diary row. The emulator has no Open Food Facts food with a brand to search for, so these scenarios use the CoFID seed, whose title is its name; bUnit and the stories cover branded titles, the brand line and the pack.

  Scenario: The Foods tab heads a food's values and Serving with the title its search row shows
    Given I have opened Foods
    And I have searched for "oat"
    And I have noted the title of the first result
    When I open the first result
    Then the food's values are headed with the noted title
    When I tap Add to diary
    Then Serving is headed with the noted title

  Scenario: The dashboard row of a logged food reads as its search row did
    Given I have opened Foods
    And I have searched for "oat"
    And I have noted the title of the first result
    And I have opened the first result
    When I tap Add to diary
    And I tap Add
    And I tap View day
    Then the day has an entry headed with the noted title

  Scenario: Scan headed a product with the title Serving then shows
    Given I am on the Dashboard
    And a barcode is held in front of the camera
    When I open Scan from the navigation bar
    Then Scan shows the found product with Add to diary
    And I have noted the product's title on Scan
    When I tap Add to diary on Scan
    Then Serving is headed with the noted title
