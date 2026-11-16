Feature: Diary journeys
  The three routes that add a food to the diary, each tapped through once from where a person starts to where they land: a meal's +, the Foods tab and the Scan tab. All end at the Serving screen. The scan journey has no camera on an emulator, so a Debug-build seam hands the scanner a barcode and finds the first "oat" Ingredient for it (src/CalorieTracker.App/Scanning/E2EScanSeam.cs).

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
  Scenario: Journey 2 Add from the Foods tab and land back in search with View day
    Given I have opened Foods
    And I have searched for "oat"
    And I have opened the first result
    When I tap Add to diary
    Then Serving says the meal was suggested from the time
    When I tap Add
    Then I am on Foods
    And "oat" is still typed
    And the snackbar says it was added to the suggested meal with the action "View day"

  @smoke
  Scenario: Journey 3 Add a scanned product from Scan
    Given I am on the Dashboard
    And a barcode is held in front of the camera
    When I open Scan from the navigation bar
    Then Scan shows the found product with Add to diary
    When I tap Add to diary on Scan
    Then Serving says the meal was suggested from the time
    When I tap Add
    Then I am on the Dashboard
    And the snackbar says it was added to the suggested meal with the action "Undo"
