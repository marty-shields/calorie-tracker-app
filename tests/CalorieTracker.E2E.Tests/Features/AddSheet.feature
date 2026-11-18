Feature: Add sheet
  The bar's Add button opens a sheet that guesses the meal from the time and opens search or scan for it, or adds a usual food of that meal in one tap. Which meal the time gives is covered by bUnit and Core's unit tests, since the emulator's clock is not fixed; here the sheet always has exactly one meal selected and the chosen one travels on. That the chosen meal reaches Scan is proved by its chip (below) and by Journey 3 in Journeys.feature.

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
    And I refuse the camera permission
    Then I am on Scan
    And Scan says it is adding to Lunch today

  Scenario: Android back closes the sheet and focus returns to Add
    Given I am on the Dashboard
    And I have opened the Add sheet
    When I press Android back on the Add sheet
    Then the Add sheet is closed
    And I am on the Dashboard
    And focus is back on the Add button

  Scenario: Tapping the dimmed screen closes the sheet and focus returns to Add
    Given I am on the Dashboard
    And I have opened the Add sheet
    When I tap the dimmed screen above the Add sheet
    Then the Add sheet is closed
    And I am on the Dashboard
    And focus is back on the Add button

  Scenario: Tab stays inside the open sheet and wraps at both ends
    Given I am on the Dashboard
    And I have opened the Add sheet
    When I press Tab once for each control on the Add sheet
    Then focus is in the Add sheet
    And focus is on the Add sheet's first control
    When I press Shift+Tab 1 times on the Add sheet
    Then focus is on the Add sheet's last control

  Scenario: A usual food is added in one tap from the Add sheet
    Given I have logged 35 g of the first food found by "oat" in Lunch
    When I open the Add sheet
    And I choose Lunch on the Add sheet
    Then the Add sheet lists a usual food showing "35" g and its calories
    When I tap the plus of the first usual food on the Add sheet
    Then the Add sheet is closed
    And the snackbar says it was added to Lunch with the action "Undo"
    And Lunch lists 2 entries
    When I tap Undo
    Then Lunch lists 1 entry

  @today
  Scenario: A usual food added to a folded meal leaves that meal open
    Given I am on the Dashboard
    And the app clock reads 19:30
    And I have logged 35 g of the first food found by "oat" in Lunch
    When I leave Today and come back
    Then Lunch is folded to one line with 1 item and its kcal
    When I open the Add sheet
    And I choose Lunch on the Add sheet
    And I tap the plus of the first usual food on the Add sheet
    Then the Add sheet is closed
    And Lunch is open with 2 entries shown
