import { createMaterialTopTabNavigator } from "@react-navigation/material-top-tabs";
import { FC } from "react";
import { RootNavigationStackScreenProps } from "../../RootStackNavigation";
import { AllPostScreen } from "./AllPostScreen";
import { FavoritePostScreen } from "./FavoritePostScreen";

const Tab = createMaterialTopTabNavigator();

export const ProfileScreen: FC<
    RootNavigationStackScreenProps<"Profile">
> = () => {
    return (
        <Tab.Navigator>
            <Tab.Screen
                name="AllPost"
                component={AllPostScreen}
                options={{ title: "All" }}
            />

            <Tab.Screen
                name="FavoritePost"
                component={FavoritePostScreen}
                options={{ title: "Favorite" }}
            />
        </Tab.Navigator>
    );
};
