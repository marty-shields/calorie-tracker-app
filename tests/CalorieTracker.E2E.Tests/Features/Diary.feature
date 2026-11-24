Feature: Diary
  Tapping an entry on Today opens Serving in edit mode. Save changes the amount or moves the entry to another meal; the trash icon removes it, and Today offers Undo. Each scenario adds one entry first. A meal's default fold depends on the day and the time (see Dashboard); the steps that read or tap a meal's entries open it first if it starts folded.

  Scenario: An entry opens Serving in edit mode with a named trash icon
    Given I have added 100 g of the first food found by "oat" to Lunch
    When I tap the Lunch entry
    Then Serving is in edit mode with a Save button and no Add button
    And the trash icon is named "Remove entry"
    And Serving shows the Lunch meal and day

  Scenario: Editing the amount updates the entry and the totals
    Given I have added 100 g of the first food found by "oat" to Lunch
    When I tap the Lunch entry
    And I enter an amount of "50"
    And I tap Save
    Then I am on the Dashboard
    And the Lunch entry shows "50 g"
    And the calories eaten went down

  Scenario: Changing the meal moves the entry
    Given I have added 100 g of the first food found by "oat" to Lunch
    When I tap the Lunch entry
    And I choose Dinner on Serving
    And I tap Save
    Then I am on the Dashboard
    And Lunch lists no entries
    And Dinner lists 1 entry

  Scenario: Back from edit mode leaves the entry as it was
    Given I have added 100 g of the first food found by "oat" to Lunch
    When I tap the Lunch entry
    And I enter an amount of "50"
    And I tap Back on Serving in edit mode
    Then I am on the Dashboard
    And the Lunch entry is the same as before

  Scenario: Removing an entry offers Undo, and Undo restores it exactly
    Given I have added 100 g of the first food found by "oat" to Lunch
    When I tap the Lunch entry
    And I tap the trash icon
    Then I am on the Dashboard
    And the snackbar says " removed" with the action "Undo"
    And Lunch lists no entries
    And the calories eaten went down
    When I tap Undo
    Then the Lunch entry is the same as before
    And the calories eaten are as before

  Scenario: Removing an entry and not tapping Undo leaves it removed
    Given I have added 100 g of the first food found by "oat" to Lunch
    When I tap the Lunch entry
    And I tap the trash icon
    Then the snackbar says " removed" with the action "Undo"
    And the snackbar goes away
    And Lunch lists no entries

  @today
  Scenario: A meal folds on tap, opens on tap, and is open again after leaving Today
    Given I am on the Dashboard
    And the app clock reads 12:00
    And I have added 100 g of the first food found by "oat" to Lunch
    When I tap the Lunch header
    Then Lunch is folded to one line with 1 item and its kcal
    When I tap the Lunch header
    Then Lunch is open with its entry shown
    When I tap the Lunch header
    And I leave Today and come back
    Then Lunch is open with its entry shown
