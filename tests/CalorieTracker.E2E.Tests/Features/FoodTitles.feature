Feature: Food titles
  A food reads the same wherever it is listed: the title Core works out (the brand and name, see FoodDisplay) heads the search row, the Serving screen and the diary row. The emulator has no Open Food Facts food with a brand to search for, so these scenarios use the CoFID seed, whose title is its name; bUnit and the stories cover branded titles, the brand line and the pack.

  Scenario: Serving is headed with the title the search row shows
    Given I have opened Foods
    And I have searched for "oat"
    And I have noted the title of the first result
    When I open the first result
    Then Serving is headed with the noted title

  Scenario: The dashboard row of a logged food reads as its search row did
    Given I have opened Foods
    And I have searched for "oat"
    And I have noted the title of the first result
    And I have opened the first result
    When I tap Add
    Then the day has an entry headed with the noted title

  Scenario: A scanned product opens Serving headed with its food's title
    Given I have opened Foods
    And I have searched for "oat"
    And I have noted the title of the first result
    And I go back to the Dashboard
    And a barcode is held in front of the camera
    When I open Scan from the Add sheet
    Then Serving is headed with the noted title
