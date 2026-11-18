Feature: Dashboard
  The Dashboard, titled Today in the design, is the day diary: a date strip, a compact card with the day's calories and macros, and four meals each with a +. A meal's + adds a food to that meal. A fresh install has no target and nothing logged.
  On today a meal is open during its time window (breakfast 05:00-11:00, lunch 11:00-15:00, dinner 17:00-21:00), shows only its header before it, and folds to one line after it; snacks and every other day are always open. The Debug app's clock is pinned with "the app clock reads" (src/CalorieTracker.App/Diagnostics/E2EClockSeam.cs), so these scenarios do not depend on the time the suite runs; the @today tag keeps them away from midnight. Steps that read a meal's entries open a folded meal first.

  Scenario: First run shows nothing logged and a + on each meal
    Given I am on the Dashboard
    Then the Dashboard shows "Nothing logged today"
    And every meal has a + button

  Scenario: A food added through a meal's + shows under that meal and updates the totals
    Given I am on the Dashboard
    And I have opened search to add to Lunch
    And I have searched for "oat"
    And I have opened the first result
    When I tap Add
    Then I am on the Dashboard
    And Lunch lists 1 entry with a kcal total
    And the other meals list no entries
    And the calories eaten are above 0
    And the Dashboard no longer says nothing is logged

  Scenario: With no target the ring shows the calories eaten
    Given I am on the Dashboard
    Then the ring is captioned "kcal eaten"
    And the Dashboard says "No target set"
    When I tap Set a target
    Then I am on Settings

  Scenario: The date strip changes the day and a future day looks like any other
    Given I am on the Dashboard
    When I tap the next day
    Then the date strip says "Tomorrow"
    And the Dashboard shows "Nothing logged this day"
    And every meal has a + button
    When I tap the date strip label
    Then the date strip says "Today"
    And the Dashboard shows "Nothing logged today"

  @today
  Scenario: At 09:00 breakfast is open and the later meals wait for their window
    Given I am on the Dashboard
    And the app clock reads 09:00
    Then Breakfast is open and marked Now
    And Lunch shows "from 11:00" with a + and no entries
    And Dinner shows "from 17:00" with a + and no entries
    And Snacks is open

  @today
  Scenario: At 19:30 earlier meals are folded to one line, dinner is open and snacks are always open
    Given I am on the Dashboard
    And the app clock reads 19:30
    And I have added 100 g of the first food found by "oat" to Breakfast
    And I have added 100 g of the first food found by "oat" to Lunch
    When I leave Today and come back
    Then Breakfast is folded to one line with 1 item and its kcal
    And Lunch is folded to one line with 1 item and its kcal
    And Dinner is open and marked Now
    And Snacks is open

  @today
  Scenario: After adding to a folded meal it is open with the new entry
    Given I am on the Dashboard
    And the app clock reads 19:30
    When I tap + on Lunch
    And I search for "oat"
    And I open the first result
    And I tap Add
    Then I am on the Dashboard
    And Lunch is open with its entry shown

  @today
  Scenario Outline: On any other day every meal is open, with no Now chip or start time
    Given I am on the Dashboard
    And the app clock reads 19:30
    When I tap the <day> day
    Then the date strip says "<label>"
    And the Dashboard shows "Nothing logged this day"
    And no meal is marked Now, shows a start time or is folded

    Examples:
      | day      | label     |
      | next     | Tomorrow  |
      | previous | Yesterday |
