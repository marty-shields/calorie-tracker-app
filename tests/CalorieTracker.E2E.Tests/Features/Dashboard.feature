Feature: Dashboard
  Today (the DashboardPage in code) is the day diary: a date strip, a compact card with the day's calories and macros, and four meals each with a +. A meal's + adds a food to that meal. A fresh install has no target and nothing logged.
  Every meal (Breakfast, Lunch, Dinner and Snacks) folds to one line, or opens again, when its header is tapped. The defaults: on a past or future day every meal starts folded; on Today the meal whose window holds the time now (Breakfast 06:00 to 10:59, Lunch 11:00 to 14:59, Dinner 15:00 to 19:59) and Snacks start open. There is no Now chip or start time. Fold state is not stored: leaving Today or changing day applies the defaults again. The scenarios pin the Debug app's clock with "the app clock reads" (src/CalorieTracker.App/Diagnostics/E2EClockSeam.cs); the @today tag keeps scenarios that log today away from midnight.

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
  Scenario: At 12:00 Lunch and Snacks start open and Breakfast and Dinner start folded
    Given I am on the Dashboard
    And the app clock reads 12:00
    Then the meals open on the Dashboard are Lunch, Snacks
    And Breakfast is folded to one line saying "Nothing logged"
    And Dinner is folded to one line saying "Nothing logged"
    And no meal is marked Now or shows a start time

  @today
  Scenario Outline: An open meal on Today folds and opens when its header is tapped
    Given I am on the Dashboard
    And the app clock reads 12:00
    And I have added 100 g of the first food found by "oat" to <meal>
    Then <meal> is open with its entry shown
    When I tap the <meal> header
    Then <meal> is folded to one line with 1 item and its kcal
    When I tap the <meal> header
    Then <meal> is open with its entry shown

    Examples:
      | meal   |
      | Lunch  |
      | Snacks |

  @today
  Scenario: At 15:33 Dinner and Snacks start open, and they fold on tap with no Now chip or start time
    Given I am on the Dashboard
    And the app clock reads 15:33
    Then the meals open on the Dashboard are Dinner, Snacks
    And no meal is marked Now or shows a start time
    When I tap the Dinner header
    And I tap the Snacks header
    Then Dinner is folded to one line saying "Nothing logged"
    And Snacks is folded to one line saying "Nothing logged"

  @today
  Scenario: A folded meal on Today is open again after leaving Today
    Given I am on the Dashboard
    And the app clock reads 12:00
    And I have added 100 g of the first food found by "oat" to Lunch
    When I tap the Lunch header
    Then Lunch is folded to one line with 1 item and its kcal
    When I leave Today and come back
    Then Lunch is open with its entry shown

  @today
  Scenario: After adding to a meal from search, Today opens with that meal's default fold
    Given I am on the Dashboard
    And the app clock reads 12:00
    When I tap the Breakfast header
    Then Breakfast is open
    When I tap + on Breakfast
    And I search for "oat"
    And I open the first result
    And I tap Add
    Then I am on the Dashboard
    And Breakfast is folded to one line with 1 item and its kcal

  Scenario Outline: On any other day every meal starts folded and opens on tap
    Given I am on the Dashboard
    When I tap the <day> day
    Then the date strip says "<label>"
    And the Dashboard shows "Nothing logged this day"
    And every meal is folded
    And no meal is marked Now or shows a start time
    When I tap the Dinner header
    Then Dinner is open
    When I tap the Dinner header
    Then Dinner is folded to one line saying "Nothing logged"

    Examples:
      | day      | label     |
      | next     | Tomorrow  |
      | previous | Yesterday |
