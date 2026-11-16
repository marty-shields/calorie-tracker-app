Feature: Dashboard
  The Dashboard is the day diary: a date strip, the day's calories, and four meals each with a +. A meal's + adds a food to that meal. A fresh install has no target and nothing logged.

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
    When I tap Set one in Settings
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
