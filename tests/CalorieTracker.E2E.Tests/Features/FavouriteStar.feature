Feature: Favourite star
  A star in the top bar of Serving (add and edit mode) makes the food a favourite, or removes it. The icon changes at once and a snackbar says what happened. Result rows have no star.

  Scenario: Starring a food on Serving keeps it starred when Serving is opened again
    Given I have opened Serving for the first food found by "oat" with Lunch preset
    Then the star on Serving is outlined and named "Add to favourites"
    When I tap the star on Serving
    Then the star on Serving is filled and named "Remove from favourites"
    And the snackbar says "Added to favourites"
    When I go back with the back arrow
    And I open Serving again for the first food found by "oat"
    Then the star on Serving is filled and named "Remove from favourites"

  Scenario: Tapping a filled star removes the favourite, and Undo stars it again
    Given I have opened Serving for the first food found by "oat" with Lunch preset
    And I have starred the food on Serving
    When I tap the star on Serving
    Then the star on Serving is outlined and named "Add to favourites"
    And the snackbar says "Removed from favourites" with the action "Undo"
    When I tap Undo
    Then the star on Serving is filled and named "Remove from favourites"

  Scenario: Unstarring a food keeps it unstarred when Serving is opened again
    Given I have opened Serving for the first food found by "oat" with Lunch preset
    And I have starred the food on Serving
    When I tap the star on Serving
    And I go back with the back arrow
    And I open Serving again for the first food found by "oat"
    Then the star on Serving is outlined and named "Add to favourites"

  Scenario: A food is starred from a Today entry
    Given I have added 100 g of the first food found by "oat" to Lunch
    When I tap the Lunch entry
    Then Serving is in edit mode with a Save button and no Add button
    And the star comes before the trash icon on Serving
    When I tap the star on Serving
    Then the star on Serving is filled and named "Remove from favourites"
    And the snackbar says "Added to favourites"
    When I tap Save
    Then I am on the Dashboard
    And the Lunch entry shows "100 g"
