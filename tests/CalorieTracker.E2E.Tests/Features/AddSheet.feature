Feature: Add sheet
  The bar's Add button opens a sheet that guesses the meal from the time and opens search or scan for it. Which meal the time gives is covered by bUnit and Core's unit tests, since the emulator's clock is not fixed; here the sheet always has exactly one meal selected and the chosen one travels on.

  @smoke
  Scenario: Add opens a sheet with one meal selected
    Given I am on the Dashboard
    When I open the Add sheet
    Then the Add sheet is titled "Add food" with the meals Breakfast, Lunch, Dinner and Snacks
    And the Add sheet has exactly one meal selected

  Scenario: Search foods opens search for the chosen meal
    Given I am on the Dashboard
    When I open the Add sheet
    And I choose Lunch on the Add sheet
    And I tap Search foods on the Add sheet
    Then search is titled "Add to Lunch" with no navigation bar

  Scenario: Scan barcode opens Scan
    Given I am on the Dashboard
    When I open the Add sheet
    And I choose Lunch on the Add sheet
    And I tap Scan barcode on the Add sheet
    Then I am on Scan

  Scenario: Android back closes the sheet and focus returns to Add
    Given I am on the Dashboard
    And I have opened the Add sheet
    When I press Android back on the Add sheet
    Then the Add sheet is closed
    And I am on the Dashboard
    And focus is back on the Add button
