Feature: Diary journeys
  The three routes that add a food to the diary, each tapped through once from where a person starts to where they land: a meal's +, the Add sheet's Search foods and its Scan barcode. All end at the Serving screen. The scan journey has no camera on an emulator, so a Debug-build seam hands the scanner a barcode and finds the top search result for "oat" for it (src/CalorieTracker.App/Scanning/E2EScanSeam.cs).

  @smoke
  Scenario: Journey 1 Add from a meal plus and land on the day with Undo
    Given I am on the Dashboard
    When I tap + on Lunch
    And I search for "oat"
    And I open the first result
    Then Serving has the Lunch meal chosen
    When I tap Add
    Then I am on the Dashboard
    And Lunch lists 1 entry
    And the snackbar says it was added to Lunch with the action "Undo"

  @smoke
  Scenario: Journey 2 Add from the Add sheet's Search foods and land on the day with Undo
    Given I am on the Dashboard
    When I open the Add sheet
    And I choose Dinner on the Add sheet
    And I tap Search foods on the Add sheet
    And I search for "oat"
    And I open the first result
    Then Serving has the Dinner meal chosen
    When I tap Add
    Then I am on the Dashboard
    And Dinner lists 1 entry
    And the snackbar says it was added to Dinner with the action "Undo"

  @smoke
  Scenario: Journey 3 Add a scanned product from Scan
    Given I am on the Dashboard
    And a barcode is held in front of the camera
    When I open the Add sheet
    And I choose Snacks on the Add sheet
    And I tap Scan barcode on the Add sheet
    Then Serving has the Snacks meal chosen
    When I tap Add
    Then I am on the Dashboard
    And the snackbar says it was added to Snacks with the action "Undo"
    And Snacks lists 1 entry
