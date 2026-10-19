import { type ReactNode, useState } from 'react';
import { YStack } from 'tamagui';

/** Use natural heights in a scrolling column; share only the width on desktop. */
export function ResponsiveWidgetPair({ children, testID }: {
  children: [ReactNode, ReactNode];
  testID?: string;
}) {
  const [width, setWidth] = useState(0);
  const sideBySide = width >= 900;

  return (
    <YStack
      testID={testID}
      width="100%"
      minWidth={0}
      flexShrink={0}
      gap="$4"
      flexDirection={sideBySide ? 'row' : 'column'}
      alignItems="flex-start"
      onLayout={(event) => setWidth(event.nativeEvent.layout.width)}>
      {children.map((child, index) => (
        <YStack
          key={index}
          minWidth={0}
          width={sideBySide ? undefined : '100%'}
          flexBasis={sideBySide ? 0 : 'auto'}
          flexGrow={sideBySide ? 1 : 0}
          flexShrink={0}>
          {child}
        </YStack>
      ))}
    </YStack>
  );
}
